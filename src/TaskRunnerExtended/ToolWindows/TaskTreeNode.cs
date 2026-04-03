namespace TaskRunnerExtended.ToolWindows;

using System.Runtime.Serialization;

using Microsoft.VisualStudio.Extensibility.UI;

using TaskRunnerExtended.Models;

using IAsyncCommand = Microsoft.VisualStudio.Extensibility.UI.IAsyncCommand;

/// <summary>
/// ViewModel node for the unified tree view. Represents either a category header
/// (root node, source file node) or an individual task.
/// </summary>
[DataContract]
public class TaskTreeNode : NotifyPropertyChangedObject
{
    private string _icon;
    private string _statusIcon;
    private Models.TaskStatus _status = Models.TaskStatus.Idle;

    /// <summary>
    /// Creates a header/category node (no task, no status icon).
    /// </summary>
    public TaskTreeNode(string name, string icon = "")
    {
        Name = name;
        _icon = icon;
        _statusIcon = string.Empty;
    }

    /// <summary>
    /// Creates a task node with status icon.
    /// </summary>
    public TaskTreeNode(string name, TaskItem task)
    {
        Name = name;
        Task = task;
        _icon = TreeIcons.TaskIdle;
        _statusIcon = TreeIcons.TaskIdle;
        IsTask = true;
    }

    /// <summary>Display name in the tree.</summary>
    [DataMember]
    public string Name { get; set; }

    /// <summary>Node type icon as KnownMonikers string.</summary>
    [DataMember]
    public string Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    /// <summary>Status icon for tasks (play/spinner/error) — empty for non-task nodes.</summary>
    [DataMember]
    public string StatusIcon
    {
        get => _statusIcon;
        set => SetProperty(ref _statusIcon, value);
    }

    /// <summary>Whether this is a runnable task node (true) or a header node (false).</summary>
    [DataMember]
    public bool IsTask { get; init; }

    /// <summary>Additional info shown after the name (e.g., "(Debug)", "(Pre-Build)").</summary>
    [DataMember]
    public string Metadata { get; set; } = string.Empty;

    /// <summary>Command to start this task (bound from the ViewModel).</summary>
    [DataMember]
    public IAsyncCommand? StartCommand { get; set; }

    /// <summary>Command to stop this task (bound from the ViewModel).</summary>
    [DataMember]
    public IAsyncCommand? StopCommand { get; set; }

    /// <summary>Child nodes (source files under root, tasks under source file).</summary>
    [DataMember]
    public ObservableList<TaskTreeNode> Children { get; } = [];

    /// <summary>The underlying task item, or null for category/header nodes.</summary>
    public TaskItem? Task { get; }

    /// <summary>Current execution status.</summary>
    public Models.TaskStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            var newIcon = value switch
            {
                Models.TaskStatus.Running => TreeIcons.TaskRunning,
                Models.TaskStatus.Error => TreeIcons.TaskError,
                _ => TreeIcons.TaskIdle,
            };
            Icon = newIcon;
            StatusIcon = newIcon;
        }
    }
}
