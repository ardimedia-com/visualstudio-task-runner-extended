namespace TaskRunnerExtended.Services.Discovery;

using System.Xml.Linq;

using TaskRunnerExtended.Models;

/// <summary>
/// Discovers custom MSBuild targets with &lt;Exec Command="..."&gt; from .csproj files.
/// Supports both SDK-style (.NET Core/5+) and non-SDK-style (.NET Framework) projects.
/// </summary>
public class CsprojDiscoverer : ITaskDiscoverer
{
    public TaskSourceKind SourceKind => TaskSourceKind.Csproj;

    public Task<IReadOnlyList<TaskItem>> DiscoverAsync(string directory, CancellationToken cancellationToken)
    {
        var csprojFiles = Directory.GetFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly);
        if (csprojFiles.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<TaskItem>>([]);
        }

        var tasks = new List<TaskItem>();

        foreach (var csprojPath in csprojFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var doc = XDocument.Load(csprojPath);
                var root = doc.Root;
                if (root is null) continue;

                var isSdkStyle = root.Attribute("Sdk") is not null;
                var displayName = Path.GetFileName(csprojPath);
                var source = new TaskSource(TaskSourceKind.Csproj, csprojPath, displayName);
                var ns = root.GetDefaultNamespace();

                foreach (var target in root.Elements(ns + "Target"))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var task = ParseTarget(target, ns, source, isSdkStyle, directory);
                    if (task is not null)
                    {
                        tasks.Add(task);
                    }
                }
            }
            catch (Exception ex)
            {
                var source = new TaskSource(TaskSourceKind.Csproj, csprojPath, Path.GetFileName(csprojPath));
                tasks.Add(new TaskItem
                {
                    Label = $"Parse Error: {ex.Message}",
                    Command = string.Empty,
                    Source = source,
                    Error = ex.Message,
                });
            }
        }

        return Task.FromResult<IReadOnlyList<TaskItem>>(tasks);
    }

    private static TaskItem? ParseTarget(XElement target, XNamespace ns, TaskSource source, bool isSdkStyle, string directory)
    {
        var name = target.Attribute("Name")?.Value;
        if (string.IsNullOrEmpty(name)) return null;

        // Find <Exec Command="..."> within the target
        var exec = target.Element(ns + "Exec");
        if (exec is null) return null;

        var command = exec.Attribute("Command")?.Value;
        if (string.IsNullOrEmpty(command)) return null;

        // Build metadata from target attributes
        var metadata = BuildMetadata(target, isSdkStyle, name);

        // Determine the command to run this target
        var runCommand = isSdkStyle
            ? "dotnet"
            : "msbuild.exe";

        string[] runArgs = isSdkStyle
            ? ["msbuild", $"-t:{name}", Path.GetFileName(source.FilePath)]
            : [$"-t:{name}", Path.GetFileName(source.FilePath)];

        var workingDirectory = exec.Attribute("WorkingDirectory")?.Value ?? directory;

        return new TaskItem
        {
            Label = $"msbuild: {name}",
            Command = runCommand,
            Args = runArgs,
            WorkingDirectory = workingDirectory,
            IsShell = true,
            TaskType = Models.TaskType.Normal,
            Source = source,
            Metadata = metadata,
        };
    }

    private static string? BuildMetadata(XElement target, bool isSdkStyle, string name)
    {
        var parts = new List<string>();

        // Check BeforeTargets/AfterTargets (SDK-style)
        var beforeTargets = target.Attribute("BeforeTargets")?.Value;
        var afterTargets = target.Attribute("AfterTargets")?.Value;

        if (!string.IsNullOrEmpty(beforeTargets))
            parts.Add($"Pre-{beforeTargets}");
        else if (!string.IsNullOrEmpty(afterTargets))
            parts.Add($"Post-{afterTargets}");

        // Check .NET Framework naming convention
        if (!isSdkStyle)
        {
            if (name.Equals("BeforeBuild", StringComparison.OrdinalIgnoreCase))
                parts.Add("Pre-Build");
            else if (name.Equals("AfterBuild", StringComparison.OrdinalIgnoreCase))
                parts.Add("Post-Build");
        }

        // Check Condition for Debug/Release
        var condition = target.Attribute("Condition")?.Value;
        if (condition is not null)
        {
            if (condition.Contains("'Debug'", StringComparison.OrdinalIgnoreCase))
                parts.Add("Debug");
            else if (condition.Contains("'Release'", StringComparison.OrdinalIgnoreCase))
                parts.Add("Release");
        }

        if (!isSdkStyle)
            parts.Add(".NET Framework");

        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }
}
