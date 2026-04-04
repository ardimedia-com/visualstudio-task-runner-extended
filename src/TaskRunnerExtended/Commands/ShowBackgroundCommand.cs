namespace TaskRunnerExtended.Commands;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

using TaskRunnerExtended.Services;

/// <summary>
/// Toolbar toggle: switch to the Background tab (extension info).
/// </summary>
[VisualStudioContribution]
public class ShowBackgroundCommand : ToggleCommand
{
    public override CommandConfiguration CommandConfiguration => new("%TaskRunnerExtended.ShowBackgroundCommand.DisplayName%")
    {
        TooltipText = "%TaskRunnerExtended.ShowBackgroundCommand.TooltipText%",
        Icon = new(ImageMoniker.KnownValues.StatusInformation, IconSettings.IconOnly),
        Flags = CommandFlags.CanSelect,
    };

    public ShowBackgroundCommand(VisualStudioExtensibility extensibility)
        : base(extensibility)
    {
        ToolbarActionBus.TabChanged += OnTabChanged;
    }

    public override Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        ToolbarActionBus.RequestTabChange("Background");
        return Task.CompletedTask;
    }

    private void OnTabChanged(string tab)
    {
        IsChecked = tab == "Background";
    }
}
