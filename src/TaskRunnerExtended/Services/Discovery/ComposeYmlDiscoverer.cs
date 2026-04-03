namespace TaskRunnerExtended.Services.Discovery;

using TaskRunnerExtended.Models;

using YamlDotNet.RepresentationModel;

/// <summary>
/// Discovers Docker Compose services from compose.yml / docker-compose.yml.
/// Each service is exposed as a startable task (e.g., "docker: db").
/// </summary>
public class ComposeYmlDiscoverer : ITaskDiscoverer
{
    private static readonly string[] ComposeFileNames =
    [
        "compose.yml",
        "compose.yaml",
        "docker-compose.yml",
        "docker-compose.yaml",
    ];

    public TaskSourceKind SourceKind => TaskSourceKind.ComposeYml;

    public Task<IReadOnlyList<TaskItem>> DiscoverAsync(string directory, CancellationToken cancellationToken)
    {
        // Find the first matching compose file
        string? composeFilePath = null;
        string? displayName = null;

        foreach (var fileName in ComposeFileNames)
        {
            var path = Path.Combine(directory, fileName);
            if (File.Exists(path))
            {
                composeFilePath = path;
                displayName = fileName;
                break;
            }
        }

        if (composeFilePath is null)
        {
            return Task.FromResult<IReadOnlyList<TaskItem>>([]);
        }

        try
        {
            var yaml = File.ReadAllText(composeFilePath);
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));

            if (stream.Documents.Count == 0 ||
                stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                return Task.FromResult<IReadOnlyList<TaskItem>>([]);
            }

            // Find "services" key
            YamlMappingNode? servicesNode = null;
            foreach (var entry in root.Children)
            {
                if (entry.Key is YamlScalarNode keyNode && keyNode.Value == "services" &&
                    entry.Value is YamlMappingNode mappingNode)
                {
                    servicesNode = mappingNode;
                    break;
                }
            }

            if (servicesNode is null)
            {
                return Task.FromResult<IReadOnlyList<TaskItem>>([]);
            }

            var source = new TaskSource(TaskSourceKind.ComposeYml, composeFilePath, displayName!);
            var tasks = new List<TaskItem>();

            foreach (var service in servicesNode.Children)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (service.Key is not YamlScalarNode serviceNameNode || serviceNameNode.Value is null)
                    continue;

                var serviceName = serviceNameNode.Value;

                // Extract image name as metadata if available
                string? image = null;
                if (service.Value is YamlMappingNode serviceConfig)
                {
                    foreach (var prop in serviceConfig.Children)
                    {
                        if (prop.Key is YamlScalarNode key && key.Value == "image" &&
                            prop.Value is YamlScalarNode val)
                        {
                            image = val.Value;
                            break;
                        }
                    }
                }

                // Individual service: docker compose up <service>
                tasks.Add(new TaskItem
                {
                    Label = $"docker: {serviceName}",
                    Command = "docker",
                    Args = ["compose", "-f", displayName!, "up", serviceName],
                    WorkingDirectory = directory,
                    IsShell = true,
                    TaskType = TaskType.Background,
                    Source = source,
                    Metadata = image,
                });
            }

            // Add a "start all" task
            if (tasks.Count > 1)
            {
                tasks.Insert(0, new TaskItem
                {
                    Label = "docker: (all services)",
                    Command = "docker",
                    Args = ["compose", "-f", displayName!, "up"],
                    WorkingDirectory = directory,
                    IsShell = true,
                    TaskType = TaskType.Background,
                    Source = source,
                    Metadata = $"{tasks.Count} services",
                });
            }

            return Task.FromResult<IReadOnlyList<TaskItem>>(tasks);
        }
        catch (Exception ex)
        {
            var source = new TaskSource(TaskSourceKind.ComposeYml, composeFilePath, displayName!);
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
}
