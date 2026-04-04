namespace TaskRunnerExtended.Commands;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

using TaskRunnerExtended.Services;

/// <summary>
/// Toolbar toggle: switch to the Feedback tab (GitHub issue form).
/// </summary>
[VisualStudioContribution]
public class ShowFeedbackCommand : ToggleCommand
{
    public override CommandConfiguration CommandConfiguration => new("%TaskRunnerExtended.ShowFeedbackCommand.DisplayName%")
    {
        TooltipText = "%TaskRunnerExtended.ShowFeedbackCommand.TooltipText%",
        Icon = new(ImageMoniker.KnownValues.Feedback, IconSettings.IconAndText),
        Flags = CommandFlags.CanToggle,
    };

    public ShowFeedbackCommand(VisualStudioExtensibility extensibility)
        : base(extensibility)
    {
        ToolbarActionBus.TabChanged += OnTabChanged;
        IsChecked = ToolbarActionBus.ActiveTab == "Feedback";
    }

    public override Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        ToolbarActionBus.RequestTabChange("Feedback");
        return Task.CompletedTask;
    }

    private void OnTabChanged(string tab)
    {
        IsChecked = tab == "Feedback";
    }
}
