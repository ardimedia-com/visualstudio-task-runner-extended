namespace TaskRunnerExtended.Models;

/// <summary>
/// Represents a discovered task configuration file (e.g., a specific tasks.json or .csproj).
/// </summary>
public record TaskSource(
    TaskSourceKind Kind,
    string FilePath,
    string DisplayName);
