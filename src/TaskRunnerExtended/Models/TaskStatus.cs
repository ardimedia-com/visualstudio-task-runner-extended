namespace TaskRunnerExtended.Models;

/// <summary>
/// Execution status of a task.
/// </summary>
public enum TaskStatus
{
    /// <summary>Task is not running.</summary>
    Idle,

    /// <summary>Task is currently running.</summary>
    Running,

    /// <summary>Task exited with an error (non-zero exit code or crash).</summary>
    Error,
}
