namespace TaskRunnerExtended.Commands;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

using TaskRunnerExtended.Services;

/// <summary>
/// Toolbar toggle: switch to the Tasks tab (tree view + details).
/// </summary>
[VisualStudioContribution]
public class ShowTasksCommand : ToggleCommand
{
    public override CommandConfiguration CommandConfiguration => new("%TaskRunnerExtended.ShowTasksCommand.DisplayName%")
    {
        TooltipText = "%TaskRunnerExtended.ShowTasksCommand.TooltipText%",
        Icon = new(ImageMoniker.KnownValues.TaskList, IconSettings.IconOnly),
        Flags = CommandFlags.CanSelect,
    };

    public ShowTasksCommand(VisualStudioExtensibility extensibility)
        : base(extensibility)
    {
        IsChecked = true; // Tasks tab is active by default
        ToolbarActionBus.TabChanged += OnTabChanged;
    }

    public override Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        ToolbarActionBus.RequestTabChange("Tasks");
        return Task.CompletedTask;
    }

    private void OnTabChanged(string tab)
    {
        IsChecked = tab == "Tasks";
    }
}
