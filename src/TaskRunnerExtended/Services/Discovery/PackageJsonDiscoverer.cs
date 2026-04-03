namespace TaskRunnerExtended.Services.Discovery;

using System.Text.Json;

using TaskRunnerExtended.Models;

/// <summary>
/// Discovers npm/pnpm/yarn scripts from package.json files.
/// Detects the package manager via lock files (pnpm > yarn > npm).
/// Background task detection uses heuristics on script name and command content.
/// </summary>
public class PackageJsonDiscoverer : ITaskDiscoverer
{
    public TaskSourceKind SourceKind => TaskSourceKind.PackageJson;

    public Task<IReadOnlyList<TaskItem>> DiscoverAsync(string directory, CancellationToken cancellationToken)
    {
        var packageJsonPath = Path.Combine(directory, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            return Task.FromResult<IReadOnlyList<TaskItem>>([]);
        }

        try
        {
            var json = File.ReadAllText(packageJsonPath);
            var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            if (!doc.RootElement.TryGetProperty("scripts", out var scripts) ||
                scripts.ValueKind != JsonValueKind.Object)
            {
                return Task.FromResult<IReadOnlyList<TaskItem>>([]);
            }

            var packageManager = DetectPackageManager(directory);
            var runCommand = packageManager switch
            {
                "pnpm" => "pnpm",
                "yarn" => "yarn",
                _ => "npm",
            };

            var source = new TaskSource(TaskSourceKind.PackageJson, packageJsonPath, "package.json");
            var tasks = new List<TaskItem>();

            foreach (var script in scripts.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scriptName = script.Name;
                var scriptCommand = script.Value.GetString() ?? string.Empty;

                // Skip pre/post lifecycle scripts — they run automatically
                if (scriptName.StartsWith("pre", StringComparison.Ordinal) ||
                    scriptName.StartsWith("post", StringComparison.Ordinal))
                {
                    // But keep "prestart" etc. only if there's no matching base script
                    var baseName = scriptName.StartsWith("pre") ? scriptName[3..] : scriptName[4..];
                    if (scripts.TryGetProperty(baseName, out _))
                    {
                        continue;
                    }
                }

                var isBackground = IsLikelyBackgroundTask(scriptName, scriptCommand);
                var label = $"{runCommand}: {scriptName}";

                tasks.Add(new TaskItem
                {
                    Label = label,
                    Command = runCommand,
                    Args = ["run", scriptName],
                    WorkingDirectory = directory,
                    IsShell = true,
                    TaskType = isBackground ? TaskType.Background : TaskType.Normal,
                    Source = source,
                    Metadata = isBackground ? "background" : null,
                });
            }

            return Task.FromResult<IReadOnlyList<TaskItem>>(tasks);
        }
        catch (JsonException ex)
        {
            var source = new TaskSource(TaskSourceKind.PackageJson, packageJsonPath, "package.json");
            return Task.FromResult<IReadOnlyList<TaskItem>>(
            [
                new TaskItem
                {
                    Label = $"Parse Error: {ex.Message}",
                    Command = string.Empty,
                    Source = source,
                    Error = ex.Message,
                },
            ]);
        }
    }

    /// <summary>
    /// Detects the package manager by checking for lock files.
    /// Priority: pnpm > yarn > npm.
    /// </summary>
    private static string DetectPackageManager(string directory)
    {
        if (File.Exists(Path.Combine(directory, "pnpm-lock.yaml")))
            return "pnpm";
        if (File.Exists(Path.Combine(directory, "yarn.lock")))
            return "yarn";
        return "npm";
    }

    /// <summary>
    /// Heuristic: a script is likely a background task if its name or command
    /// suggests it runs indefinitely.
    /// </summary>
    private static bool IsLikelyBackgroundTask(string name, string command)
    {
        // Name-based heuristics
        var bgNames = new[] { "watch", "dev", "serve", "start" };
        if (bgNames.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Command-based heuristics
        var bgPatterns = new[] { "--watch", "--serve", "concurrently", "nodemon", "live-server", "webpack serve" };
        if (bgPatterns.Any(p => command.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }
}
