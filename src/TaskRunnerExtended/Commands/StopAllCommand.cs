namespace TaskRunnerExtended.Commands;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

using TaskRunnerExtended.Services;

/// <summary>
/// Toolbar command: Stop all running tasks.
/// </summary>
[VisualStudioContribution]
public class StopAllCommand : Command
{
    public override CommandConfiguration CommandConfiguration => new("%TaskRunnerExtended.StopAllCommand.DisplayName%")
    {
        TooltipText = "%TaskRunnerExtended.StopAllCommand.TooltipText%",
        Icon = new(ImageMoniker.KnownValues.Stop, IconSettings.IconOnly),
    };

    public StopAllCommand(VisualStudioExtensibility extensibility)
        : base(extensibility)
    {
    }

    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        ToolbarActionBus.RequestStopAll();
    }
}
