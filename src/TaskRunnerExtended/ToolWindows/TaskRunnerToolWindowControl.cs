namespace TaskRunnerExtended.ToolWindows;

using Microsoft.VisualStudio.Extensibility.UI;

/// <summary>
/// Remote UI control for the Task Runner Extended tool window.
/// </summary>
public class TaskRunnerToolWindowControl : RemoteUserControl
{
    public TaskRunnerToolWindowControl(object? dataContext)
        : base(dataContext)
    {
    }
}
