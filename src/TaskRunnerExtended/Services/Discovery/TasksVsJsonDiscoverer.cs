namespace TaskRunnerExtended.Services.Discovery;

using System.Text.Json;

using TaskRunnerExtended.Models;

/// <summary>
/// Discovers tasks from .vs/tasks.vs.json (Visual Studio native format).
/// Primarily used for unrecognized codebases (CMake, Makefiles, etc.).
/// </summary>
public class TasksVsJsonDiscoverer : ITaskDiscoverer
{
    public TaskSourceKind SourceKind => TaskSourceKind.TasksVsJson;

    public Task<IReadOnlyList<TaskItem>> DiscoverAsync(string directory, CancellationToken cancellationToken)
    {
        var tasksVsJsonPath = Path.Combine(directory, ".vs", "tasks.vs.json");
        if (!File.Exists(tasksVsJsonPath))
        {
            return Task.FromResult<IReadOnlyList<TaskItem>>([]);
        }

        try
        {
            var json = File.ReadAllText(tasksVsJsonPath);
            var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            var source = new TaskSource(TaskSourceKind.TasksVsJson, tasksVsJsonPath, ".vs/tasks.vs.json");
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
            var source = new TaskSource(TaskSourceKind.TasksVsJson, tasksVsJsonPath, ".vs/tasks.vs.json");
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

    private static TaskItem? ParseTask(JsonElement element, TaskSource source, string directory)
    {
        // taskLabel (note: different from tasks.json which uses "label")
        var label = element.TryGetProperty("taskLabel", out var labelProp) && labelProp.ValueKind == JsonValueKind.String
            ? labelProp.GetString()!
            : element.TryGetProperty("taskName", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                ? nameProp.GetString()!
                : null;

        if (string.IsNullOrEmpty(label)) return null;

        // command
        var command = element.TryGetProperty("command", out var cmdProp) && cmdProp.ValueKind == JsonValueKind.String
            ? cmdProp.GetString()!
            : string.Empty;

        // type: "msbuild" tasks don't have a command
        var type = element.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String
            ? typeProp.GetString()
            : null;

        if (string.IsNullOrEmpty(command) && type != "msbuild")
        {
            return null;
        }

        // For msbuild type, construct the command
        if (type == "msbuild")
        {
            command = "dotnet";
            var msbuildConfig = element.TryGetProperty("msbuildConfiguration", out var configProp) && configProp.ValueKind == JsonValueKind.String
                ? configProp.GetString()
                : null;

            var args = new List<string> { "build" };
            if (msbuildConfig is not null)
            {
                args.Add($"-c");
                args.Add(msbuildConfig);
            }

            return new TaskItem
            {
                Label = label,
                Command = command,
                Args = args.ToArray(),
                WorkingDirectory = element.TryGetProperty("workingDirectory", out var wdProp) && wdProp.ValueKind == JsonValueKind.String
                    ? wdProp.GetString()
                    : directory,
                IsShell = true,
                TaskType = TaskType.Normal,
                Source = source,
                Metadata = type,
            };
        }

        // args
        var argsList = new List<string>();
        if (element.TryGetProperty("args", out var argsProp) && argsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var arg in argsProp.EnumerateArray())
            {
                if (arg.ValueKind == JsonValueKind.String)
                    argsList.Add(arg.GetString()!);
            }
        }

        // appliesTo metadata
        var appliesTo = element.TryGetProperty("appliesTo", out var atProp) && atProp.ValueKind == JsonValueKind.String
            ? atProp.GetString()
            : null;

        return new TaskItem
        {
            Label = label,
            Command = command,
            Args = argsList.ToArray(),
            WorkingDirectory = element.TryGetProperty("workingDirectory", out var cwdProp) && cwdProp.ValueKind == JsonValueKind.String
                ? cwdProp.GetString()
                : directory,
            IsShell = true,
            TaskType = TaskType.Normal,
            Source = source,
            Metadata = appliesTo is not null ? $"appliesTo: {appliesTo}" : type,
        };
    }
}
