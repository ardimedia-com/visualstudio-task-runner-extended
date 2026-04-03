namespace TaskRunnerExtended.Services.Discovery;

using System.Text.Json;

using TaskRunnerExtended.Models;

/// <summary>
/// Discovers tasks from .vscode/tasks.json files.
/// Phase 1: label, command, args, cwd, isBackground, type, group.
/// </summary>
public class TasksJsonDiscoverer : ITaskDiscoverer
{
    public TaskSourceKind SourceKind => TaskSourceKind.TasksJson;

    public Task<IReadOnlyList<TaskItem>> DiscoverAsync(string directory, CancellationToken cancellationToken)
    {
        var tasksJsonPath = Path.Combine(directory, ".vscode", "tasks.json");
        if (!File.Exists(tasksJsonPath))
        {
            return Task.FromResult<IReadOnlyList<TaskItem>>([]);
        }

        try
        {
            var json = File.ReadAllText(tasksJsonPath);
            var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            var source = new TaskSource(TaskSourceKind.TasksJson, tasksJsonPath, ".vscode/tasks.json");
            var tasks = new List<TaskItem>();

            if (doc.RootElement.TryGetProperty("tasks", out var tasksArray) &&
                tasksArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var taskElement in tasksArray.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var task = ParseTask(taskElement, source, directory);
                    if (task is not null)
                    {
                        tasks.Add(task);
                    }
                }
            }

            return Task.FromResult<IReadOnlyList<TaskItem>>(tasks);
        }
        catch (JsonException ex)
        {
            var source = new TaskSource(TaskSourceKind.TasksJson, tasksJsonPath, ".vscode/tasks.json");
            var errorItem = new TaskItem
            {
                Label = $"Parse Error: {ex.Message}",
                Command = string.Empty,
                Source = source,
                Error = ex.Message,
            };
            return Task.FromResult<IReadOnlyList<TaskItem>>([errorItem]);
        }
    }

    private static TaskItem? ParseTask(JsonElement element, TaskSource source, string directory)
    {
        // label is required
        if (!element.TryGetProperty("label", out var labelProp) ||
            labelProp.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var label = labelProp.GetString()!;

        // command — required for executable tasks (compound tasks only have dependsOn)
        var command = element.TryGetProperty("command", out var cmdProp) &&
                      cmdProp.ValueKind == JsonValueKind.String
            ? cmdProp.GetString()!
            : string.Empty;

        // Compound tasks (dependsOn without command) — skip in Phase 1
        if (string.IsNullOrEmpty(command))
        {
            return null;
        }

        // args
        var args = ParseArgs(element);

        // type: "shell" (default) or "process"
        var isShell = true;
        if (element.TryGetProperty("type", out var typeProp) &&
            typeProp.ValueKind == JsonValueKind.String &&
            typeProp.GetString() is "process")
        {
            isShell = false;
        }

        // cwd / options.cwd
        string? cwd = null;
        if (element.TryGetProperty("options", out var options) &&
            options.TryGetProperty("cwd", out var cwdProp) &&
            cwdProp.ValueKind == JsonValueKind.String)
        {
            cwd = cwdProp.GetString();
        }

        // isBackground
        var isBackground = element.TryGetProperty("isBackground", out var bgProp) &&
                           bgProp.ValueKind == JsonValueKind.True;

        // group metadata
        string? metadata = null;
        if (element.TryGetProperty("group", out var groupProp))
        {
            if (groupProp.ValueKind == JsonValueKind.String)
            {
                metadata = groupProp.GetString();
            }
            else if (groupProp.ValueKind == JsonValueKind.Object &&
                     groupProp.TryGetProperty("kind", out var kindProp) &&
                     kindProp.ValueKind == JsonValueKind.String)
            {
                metadata = kindProp.GetString();
            }
        }

        return new TaskItem
        {
            Label = label,
            Command = command,
            Args = args,
            // Default CWD = directory that was scanned (workspace root for solution-level tasks.json)
            WorkingDirectory = cwd ?? directory,
            IsShell = isShell,
            TaskType = isBackground ? Models.TaskType.Background : Models.TaskType.Normal,
            Source = source,
            Metadata = metadata,
        };
    }

    private static string[] ParseArgs(JsonElement element)
    {
        if (!element.TryGetProperty("args", out var argsProp) ||
            argsProp.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return argsProp.EnumerateArray()
            .Where(a => a.ValueKind == JsonValueKind.String)
            .Select(a => a.GetString()!)
            .ToArray();
    }
}
