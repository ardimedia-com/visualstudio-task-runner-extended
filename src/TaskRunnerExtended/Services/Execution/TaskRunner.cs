namespace TaskRunnerExtended.Services.Execution;

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.VisualStudio.Extensibility;

using TaskRunnerExtended.Models;

/// <summary>
/// Manages running task processes. Handles starting tasks via Process.Start,
/// streaming output to VS Output Window Panes, and stopping tasks via Job Objects.
/// </summary>
public class TaskRunner : IDisposable
{
    private readonly VisualStudioExtensibility _extensibility;
    private readonly Dictionary<string, RunningTask> _runningTasks = [];
    private readonly Dictionary<string, Microsoft.VisualStudio.Extensibility.Documents.OutputChannel> _outputChannels = [];
    private Microsoft.VisualStudio.Extensibility.Documents.OutputChannel? _diagnosticsChannel;
    private bool _disposed;

    public TaskRunner(VisualStudioExtensibility extensibility)
    {
        _extensibility = extensibility;
    }

    /// <summary>
    /// Starts a task and streams its output to a dedicated Output Window Pane.
    /// </summary>
    /// <param name="task">The task to start.</param>
    /// <param name="workspaceFolder">Workspace root for variable resolution.</param>
    /// <returns>True if the task was started successfully.</returns>
    public async Task<bool> StartAsync(TaskItem task, string workspaceFolder)
    {
        var taskKey = GetTaskKey(task);

        // If already running, don't start again
        if (_runningTasks.ContainsKey(taskKey))
        {
            return false;
        }

        // Resolve variables
        var (command, args) = VariableResolver.ResolveCommand(
            task.Command, task.Args, workspaceFolder);

        var cwd = task.WorkingDirectory is not null
            ? VariableResolver.Resolve(task.WorkingDirectory, workspaceFolder)
            : workspaceFolder;

        // Build ProcessStartInfo
        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = cwd,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (task.IsShell)
        {
            // Shell mode: required for commands like npm, npx, yarn (they are .cmd scripts)
            startInfo.FileName = "cmd.exe";
            var fullCommand = args.Length > 0
                ? $"{command} {string.Join(' ', args)}"
                : command;
            startInfo.Arguments = $"/c {fullCommand}";
        }
        else
        {
            // Process mode: direct execution
            startInfo.FileName = command;
            startInfo.Arguments = string.Join(' ', args);
        }

        // Suppress ANSI colors and browser launch for cleaner output
        startInfo.Environment["NO_COLOR"] = "1";
        startInfo.Environment["FORCE_COLOR"] = "0";
        startInfo.Environment["DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER"] = "1";

        // Add environment variables
        if (task.EnvironmentVariables is not null)
        {
            foreach (var (key, value) in task.EnvironmentVariables)
            {
                startInfo.Environment[key] = value;
            }
        }

        try
        {
            // Get or create output channel (keyed by unique task key to avoid collisions)
            var channelKey = taskKey;
            if (!_outputChannels.TryGetValue(channelKey, out var outputChannel))
            {
                var channelName = $"Task: {task.Label} [{task.Source.DisplayName}]";
                outputChannel = await _extensibility.Views().Output
                    .CreateOutputChannelAsync(channelName, default)
                    .ConfigureAwait(false);
                _outputChannels[channelKey] = outputChannel;
            }

            // Show the output channel first
            await outputChannel.Writer.WriteLineAsync($"[Task Runner Extended] Starting task: {task.Label}")
                .ConfigureAwait(false);
            await outputChannel.Writer.WriteLineAsync($"> {startInfo.FileName} {startInfo.Arguments}")
                .ConfigureAwait(false);
            await outputChannel.Writer.WriteLineAsync($"  Working directory: {cwd}")
                .ConfigureAwait(false);
            await outputChannel.Writer.WriteLineAsync(string.Empty).ConfigureAwait(false);

            // Start process with Job Object
            var processManager = new ProcessTreeManager();
            Process process;
            try
            {
                process = processManager.Start(startInfo);
            }
            catch (Exception ex)
            {
                await outputChannel.Writer.WriteLineAsync($"[ERROR] Failed to start process: {ex.Message}")
                    .ConfigureAwait(false);
                processManager.Dispose();
                return false;
            }

            var problemMatcher = new ProblemMatcher();
            var runningTask = new RunningTask(task, process, processManager, outputChannel, problemMatcher);
            _runningTasks[taskKey] = runningTask;

            // Stream output async (fire-and-forget)
            _ = StreamOutputAsync(runningTask);

            return true;
        }
        catch (Exception ex)
        {
            // Log error to diagnostics
            try
            {
                _diagnosticsChannel ??= await _extensibility.Views().Output
                    .CreateOutputChannelAsync("Task Runner Extended - Diagnostics", default)
                    .ConfigureAwait(false);
                await _diagnosticsChannel.Writer.WriteLineAsync($"Failed to start '{task.Label}': {ex.Message}")
                    .ConfigureAwait(false);
            }
            catch
            {
                // Diagnostics channel unavailable — silently ignore
            }

            return false;
        }
    }

    /// <summary>
    /// Stops a running task (graceful shutdown, then force kill).
    /// </summary>
    public async Task StopAsync(TaskItem task, int gracefulTimeoutMs = 5000)
    {
        var taskKey = GetTaskKey(task);
        if (!_runningTasks.TryGetValue(taskKey, out var running))
        {
            return;
        }

        running.WasStopped = true;
        await running.ProcessManager.StopAsync(gracefulTimeoutMs).ConfigureAwait(false);
        _runningTasks.Remove(taskKey);

        await running.OutputChannel.Writer.WriteLineAsync(string.Empty).ConfigureAwait(false);
        await running.OutputChannel.Writer.WriteLineAsync("--- Task stopped ---").ConfigureAwait(false);
    }

    /// <summary>
    /// Stops all running tasks.
    /// </summary>
    public async Task StopAllAsync()
    {
        var tasks = _runningTasks.Values.ToList();
        _runningTasks.Clear();

        foreach (var running in tasks)
        {
            try
            {
                await running.ProcessManager.StopAsync(3000).ConfigureAwait(false);
            }
            catch
            {
                running.ProcessManager.ForceKill();
            }
        }
    }

    /// <summary>
    /// Checks if a specific task is currently running.
    /// </summary>
    public bool IsRunning(TaskItem task)
    {
        var taskKey = GetTaskKey(task);
        return _runningTasks.TryGetValue(taskKey, out var running)
               && !running.Process.HasExited;
    }

    /// <summary>
    /// Closes (disposes) the output channel for a task. A new channel is created on next start.
    /// </summary>
    public void CloseOutput(TaskItem task)
    {
        var taskKey = GetTaskKey(task);
        if (_outputChannels.TryGetValue(taskKey, out var channel))
        {
            channel.Dispose();
            _outputChannels.Remove(taskKey);
        }
    }

    /// <summary>
    /// Returns the exit code of a completed task, or null if still running.
    /// </summary>
    public int? GetExitCode(TaskItem task)
    {
        var taskKey = GetTaskKey(task);
        if (_runningTasks.TryGetValue(taskKey, out var running) && running.Process.HasExited)
        {
            return running.Process.ExitCode;
        }

        return null;
    }

    /// <summary>
    /// Event raised when a task's status changes (started, exited, error).
    /// </summary>
    public event Action<TaskItem, Models.TaskStatus>? TaskStatusChanged;

    private async Task StreamOutputAsync(RunningTask running)
    {
        var process = running.Process;
        var writer = running.OutputChannel.Writer;

        try
        {
            // Read stdout and stderr in parallel
            var matcher = running.ProblemMatcher;

            var stdoutTask = Task.Run(async () =>
            {
                while (await process.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    var clean = StripAnsi(line);
                    matcher.AnalyzeLine(clean);
                    await writer.WriteLineAsync(clean).ConfigureAwait(false);
                }
            });

            var stderrTask = Task.Run(async () =>
            {
                while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    var clean = StripAnsi(line);
                    matcher.AnalyzeLine(clean);
                    await writer.WriteLineAsync(clean).ConfigureAwait(false);
                }
            });

            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            // Report completion with problem summary
            var exitCode = process.ExitCode;
            await writer.WriteLineAsync(string.Empty).ConfigureAwait(false);

            var problemSummary = matcher.GetSummary();
            if (!string.IsNullOrEmpty(problemSummary))
            {
                await writer.WriteLineAsync($"--- Problems: {problemSummary} ---").ConfigureAwait(false);
            }

            await writer.WriteLineAsync($"--- Task exited with code {exitCode} ---").ConfigureAwait(false);

            // Don't report Error if the task was manually stopped (force kill gives non-zero exit code)
            if (!running.WasStopped)
            {
                var status = (exitCode != 0 || matcher.HasErrors)
                    ? Models.TaskStatus.Error
                    : Models.TaskStatus.Idle;
                TaskStatusChanged?.Invoke(running.Task, status);
            }

            // Clean up
            var taskKey = GetTaskKey(running.Task);
            _runningTasks.Remove(taskKey);
            running.ProcessManager.Dispose();
        }
        catch (Exception)
        {
            TaskStatusChanged?.Invoke(running.Task, Models.TaskStatus.Error);
        }
    }

    private static string GetTaskKey(TaskItem task) =>
        $"{task.Source.FilePath}::{task.Label}";

    /// <summary>
    /// Strips ANSI escape codes (colors, cursor, etc.) from a string.
    /// </summary>
    private static string StripAnsi(string input) =>
        Regex.Replace(input, @"\x1B\[[0-9;]*[A-Za-z]|\x1B\].*?\x07", string.Empty);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var running in _runningTasks.Values)
        {
            running.ProcessManager.Dispose();
            running.Process.Dispose();
        }

        _runningTasks.Clear();
    }

    private record RunningTask(
        TaskItem Task,
        Process Process,
        ProcessTreeManager ProcessManager,
        Microsoft.VisualStudio.Extensibility.Documents.OutputChannel OutputChannel,
        ProblemMatcher ProblemMatcher)
    {
        /// <summary>Set to true when Stop is called manually — prevents Error status on non-zero exit.</summary>
        public bool WasStopped { get; set; }
    };
}
