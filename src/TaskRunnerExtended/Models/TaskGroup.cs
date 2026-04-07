namespace TaskRunnerExtended.Models;

/// <summary>
/// A named group of task references that can be started together.
/// </summary>
public class TaskGroup
{
    public required string Name { get; set; }
    public string Icon { get; set; } = "play";
    public List<TaskGroupEntry> Tasks { get; set; } = [];
}

/// <summary>
/// A reference to a task within a group.
/// </summary>
public class TaskGroupEntry
{
    /// <summary>Unique task identifier: "{relativePath}::{label}". Primary lookup key.</summary>
    public string? Id { get; set; }

    /// <summary>Relative path to the source file (e.g., ".vscode/tasks.json", "package.json"). Legacy, used for backward compat.</summary>
    public required string Source { get; set; }

    /// <summary>Task label within that source. Legacy, used for backward compat.</summary>
    public required string Task { get; set; }

    /// <summary>"parallel" or "sequence".</summary>
    public string StartOrder { get; set; } = "parallel";

    /// <summary>Execution order for sequential tasks (lower = first).</summary>
    public int Order { get; set; }
}

/// <summary>
/// Root config file structure for task-runner-extended-am.json.
/// </summary>
public class TaskGroupConfig
{
    public string Version { get; set; } = "1.0";
    public List<TaskGroup> Groups { get; set; } = [];
}
