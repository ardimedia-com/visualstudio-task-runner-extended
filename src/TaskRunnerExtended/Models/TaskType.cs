namespace TaskRunnerExtended.Models;

/// <summary>
/// Whether a task is expected to exit on its own or run indefinitely.
/// </summary>
public enum TaskType
{
    /// <summary>Task starts, runs, and exits on its own (e.g., dotnet build).</summary>
    Normal,

    /// <summary>Task runs indefinitely until stopped (e.g., dotnet watch, npm run watchcss).</summary>
    Background,
}
