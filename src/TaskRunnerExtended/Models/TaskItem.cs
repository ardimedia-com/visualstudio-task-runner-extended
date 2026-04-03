namespace TaskRunnerExtended.Models;

/// <summary>
/// Unified representation of a single runnable task, regardless of its source.
/// This is the core model that all discoverers produce.
/// </summary>
public class TaskItem
{
    /// <summary>Display name shown in the tree (e.g., "watchcss", "npm: buildcss", "msbuild: WatchTailwindCss").</summary>
    public required string Label { get; init; }

    /// <summary>The command to execute (e.g., "npm run watchcss", "dotnet msbuild -t:WatchTailwindCss").</summary>
    public required string Command { get; init; }

    /// <summary>Command-line arguments (separate from command for process type tasks).</summary>
    public string[] Args { get; init; } = [];

    /// <summary>Working directory. Null = use source file directory as default.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Whether the command runs in a shell (cmd.exe /c) or as a direct process.</summary>
    public bool IsShell { get; init; } = true;

    /// <summary>Whether this is a long-running background task or a normal task.</summary>
    public TaskType TaskType { get; init; } = TaskType.Normal;

    /// <summary>Additional environment variables to set for the process.</summary>
    public Dictionary<string, string>? EnvironmentVariables { get; init; }

    /// <summary>The source file this task was discovered from.</summary>
    public required TaskSource Source { get; init; }

    /// <summary>Additional metadata for display (e.g., "Debug only", "Pre-Build").</summary>
    public string? Metadata { get; init; }

    /// <summary>Error message if this task could not be parsed correctly. Non-null = not startable.</summary>
    public string? Error { get; init; }

    /// <summary>Whether this item represents a parse error rather than a real task.</summary>
    public bool IsError => Error is not null;
}
