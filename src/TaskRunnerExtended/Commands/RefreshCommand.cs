namespace TaskRunnerExtended.Commands;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

using TaskRunnerExtended.Services;

/// <summary>
/// Toolbar command: Refresh / rescan all task sources.
/// </summary>
[VisualStudioContribution]
public class RefreshCommand : Command
{
    public override CommandConfiguration CommandConfiguration => new("%TaskRunnerExtended.RefreshCommand.DisplayName%")
    {
        TooltipText = "%TaskRunnerExtended.RefreshCommand.TooltipText%",
        Icon = new(ImageMoniker.KnownValues.Refresh, IconSettings.IconOnly),
    };

    public RefreshCommand(VisualStudioExtensibility extensibility)
        : base(extensibility)
    {
    }

    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        ToolbarActionBus.RequestRefresh();
    }
}
