namespace TaskRunnerExtended.Services;

/// <summary>
/// Watches task source files for changes and triggers a debounced callback.
/// Monitors: tasks.json, package.json, *.csproj, launchSettings.json, Gruntfile.js, gulpfile.js, compose.yml,
/// task-runner-extended-am.json, task-runner-extended-am.local.json.
/// </summary>
public sealed class FileWatcherService : IDisposable
{
    private static readonly string[] WatchFilters =
    [
        "tasks.json",
        "package.json",
        "*.csproj",
        "launchSettings.json",
        "Gruntfile.js",
        "gulpfile.js",
        "gulpfile.ts",
        "compose.yml",
        "compose.yaml",
        "docker-compose.yml",
        "docker-compose.yaml",
        "task-runner-extended-am.json",
        "task-runner-extended-am.local.json",
    ];

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Action _onChange;
    private CancellationTokenSource? _debounceCts;
    private readonly int _debounceMs;
    private bool _disposed;

    /// <summary>
    /// Creates a new FileWatcherService.
    /// </summary>
    /// <param name="onChange">Callback invoked (debounced) when a watched file changes.</param>
    /// <param name="debounceMs">Debounce interval in milliseconds (default: 500).</param>
    public FileWatcherService(Action onChange, int debounceMs = 500)
    {
        _onChange = onChange;
        _debounceMs = debounceMs;
    }

    /// <summary>
    /// Starts watching the given directories for task source file changes.
    /// </summary>
    public void Watch(IEnumerable<string> directories)
    {
        Stop();

        foreach (var directory in directories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(directory)) continue;

            foreach (var filter in WatchFilters)
            {
                try
                {
                    var watcher = new FileSystemWatcher(directory, filter)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                        IncludeSubdirectories = true,
                        InternalBufferSize = 65536, // 64KB to prevent overflow during npm install
                        EnableRaisingEvents = true,
                    };

                    watcher.Changed += OnFileChanged;
                    watcher.Created += OnFileChanged;
                    watcher.Deleted += OnFileChanged;
                    watcher.Renamed += OnFileRenamed;

                    _watchers.Add(watcher);
                }
                catch
                {
                    // Directory not accessible — skip silently
                }
            }
        }
    }

    /// <summary>
    /// Stops all watchers.
    /// </summary>
    public void Stop()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
        _debounceCts?.Cancel();
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e) => DebounceTrigger();

    private void OnFileRenamed(object sender, RenamedEventArgs e) => DebounceTrigger();

    private void DebounceTrigger()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_debounceMs, token).ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                {
                    _onChange();
                }
            }
            catch (OperationCanceledException)
            {
                // Debounce cancelled by newer change — expected
            }
        }, token);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
