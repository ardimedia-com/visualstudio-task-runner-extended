namespace TaskRunnerExtended.Services.Discovery;

using System.Text.Json;

using TaskRunnerExtended.Models;

/// <summary>
/// Discovers launch profiles from Properties/launchSettings.json.
/// Only exists in .NET (Core/5+) projects.
/// </summary>
public class LaunchSettingsDiscoverer : ITaskDiscoverer
{
    public TaskSourceKind SourceKind => TaskSourceKind.LaunchSettings;

    public Task<IReadOnlyList<TaskItem>> DiscoverAsync(string directory, CancellationToken cancellationToken)
    {
        var launchSettingsPath = Path.Combine(directory, "Properties", "launchSettings.json");
        if (!File.Exists(launchSettingsPath))
        {
            return Task.FromResult<IReadOnlyList<TaskItem>>([]);
        }

        try
        {
            var json = File.ReadAllText(launchSettingsPath);
            var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            if (!doc.RootElement.TryGetProperty("profiles", out var profiles) ||
                profiles.ValueKind != JsonValueKind.Object)
            {
                return Task.FromResult<IReadOnlyList<TaskItem>>([]);
            }

            var source = new TaskSource(TaskSourceKind.LaunchSettings, launchSettingsPath, "launchSettings.json");
            var tasks = new List<TaskItem>();

            foreach (var profile in profiles.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var task = ParseProfile(profile, source, directory);
                if (task is not null)
                {
                    tasks.Add(task);
                }
            }

            return Task.FromResult<IReadOnlyList<TaskItem>>(tasks);
        }
        catch (JsonException ex)
        {
            var source = new TaskSource(TaskSourceKind.LaunchSettings, launchSettingsPath, "launchSettings.json");
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

    private static TaskItem? ParseProfile(JsonProperty profile, TaskSource source, string directory)
    {
        var name = profile.Name;
        var element = profile.Value;

        var commandName = element.TryGetProperty("commandName", out var cmdProp) && cmdProp.ValueKind == JsonValueKind.String
            ? cmdProp.GetString()
            : null;

        if (commandName is null) return null;

        string command;
        string[] args;
        var isBackground = false;

        switch (commandName)
        {
            case "Project":
                command = "dotnet";
                args = ["run", "--project", "."];
                var launchProfile = $"--launch-profile {name}";
                args = ["run", "--project", ".", "--launch-profile", name];
                break;

            case "Executable":
                var execPath = element.TryGetProperty("executablePath", out var execProp) && execProp.ValueKind == JsonValueKind.String
                    ? execProp.GetString()!
                    : string.Empty;
                var cmdLineArgs = element.TryGetProperty("commandLineArgs", out var argsProp) && argsProp.ValueKind == JsonValueKind.String
                    ? argsProp.GetString()!
                    : string.Empty;

                command = execPath;
                args = string.IsNullOrEmpty(cmdLineArgs) ? [] : cmdLineArgs.Split(' ');

                // dotnet watch is a background task
                if (execPath == "dotnet" && cmdLineArgs.Contains("watch"))
                {
                    isBackground = true;
                }
                break;

            default:
                // Docker, IISExpress, etc. — show but with metadata
                command = "dotnet";
                args = ["run", "--launch-profile", name];
                break;
        }

        if (string.IsNullOrEmpty(command)) return null;

        // Environment variables
        Dictionary<string, string>? envVars = null;
        if (element.TryGetProperty("environmentVariables", out var envProp) && envProp.ValueKind == JsonValueKind.Object)
        {
            envVars = [];
            foreach (var env in envProp.EnumerateObject())
            {
                if (env.Value.ValueKind == JsonValueKind.String)
                {
                    envVars[env.Name] = env.Value.GetString()!;
                }
            }
        }

        // Application URL for metadata
        var appUrl = element.TryGetProperty("applicationUrl", out var urlProp) && urlProp.ValueKind == JsonValueKind.String
            ? urlProp.GetString()
            : element.TryGetProperty("launchUrl", out var launchUrlProp) && launchUrlProp.ValueKind == JsonValueKind.String
                ? launchUrlProp.GetString()
                : null;

        var metadata = commandName;
        if (appUrl is not null)
        {
            metadata = $"{commandName}, {appUrl}";
        }

        return new TaskItem
        {
            Label = name,
            Command = command,
            Args = args,
            WorkingDirectory = directory,
            IsShell = true,
            TaskType = isBackground ? TaskType.Background : TaskType.Normal,
            Source = source,
            Metadata = metadata,
            EnvironmentVariables = envVars,
        };
    }
}
