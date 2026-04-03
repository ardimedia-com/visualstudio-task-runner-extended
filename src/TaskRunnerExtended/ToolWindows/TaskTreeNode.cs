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
    private bool _isNodeSelected;
    private bool _canStart;
    private bool _canStop;
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
        _canStart = true;
        _canStop = false;
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

    /// <summary>Font weight for root/header items: "Bold" or "Normal".</summary>
    [DataMember]
    public string FontWeight { get; init; } = "Normal";

    /// <summary>Additional info shown after the name (e.g., "(Debug)", "(Pre-Build)").</summary>
    [DataMember]
    public string Metadata { get; set; } = string.Empty;

    /// <summary>Command to start this task (bound from the ViewModel).</summary>
    [DataMember]
    public IAsyncCommand? StartCommand { get; set; }

    /// <summary>Command to stop this task (bound from the ViewModel).</summary>
    [DataMember]
    public IAsyncCommand? StopCommand { get; set; }

    /// <summary>Command to add this task to a group.</summary>
    [DataMember]
    public IAsyncCommand? AddToGroupCommand { get; set; }

    /// <summary>Command to remove this task from its parent group.</summary>
    [DataMember]
    public IAsyncCommand? RemoveFromGroupCommand { get; set; }

    /// <summary>Parameter for RemoveFromGroupCommand: "groupName|taskLabel".</summary>
    [DataMember]
    public string RemoveFromGroupParam { get; set; } = string.Empty;

    /// <summary>Command to delete this group.</summary>
    [DataMember]
    public IAsyncCommand? DeleteCommand { get; set; }

    /// <summary>Command to rename this group.</summary>
    [DataMember]
    public IAsyncCommand? RenameCommand { get; set; }

    /// <summary>Command to select this node (fired on right-click via vs:EventHandler).</summary>
    [DataMember]
    public IAsyncCommand? SelectCommand { get; set; }

    /// <summary>Whether this node is selected in the tree (custom selection, not WPF IsSelected).</summary>
    [DataMember]
    public bool IsNodeSelected
    {
        get => _isNodeSelected;
        set => SetProperty(ref _isNodeSelected, value);
    }

    /// <summary>Whether Start is available (task exists and is not running).</summary>
    [DataMember]
    public bool CanStart
    {
        get => _canStart;
        set => SetProperty(ref _canStart, value);
    }

    /// <summary>Whether Stop is available (task exists and is running).</summary>
    [DataMember]
    public bool CanStop
    {
        get => _canStop;
        set => SetProperty(ref _canStop, value);
    }

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
            CanStart = value != Models.TaskStatus.Running;
            CanStop = value == Models.TaskStatus.Running;
        }
    }
}
