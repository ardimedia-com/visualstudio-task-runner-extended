namespace TaskRunnerExtended.Commands;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

/// <summary>
/// Toolbar command: Collapse all tree nodes.
/// </summary>
[VisualStudioContribution]
public class CollapseAllCommand : Command
{
    public override CommandConfiguration CommandConfiguration => new("%TaskRunnerExtended.CollapseAllCommand.DisplayName%")
    {
        TooltipText = "%TaskRunnerExtended.CollapseAllCommand.TooltipText%",
        Icon = new(ImageMoniker.KnownValues.CollapseAll, IconSettings.IconOnly),
    };

    public CollapseAllCommand(VisualStudioExtensibility extensibility)
        : base(extensibility)
    {
    }

    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        // TODO: collapse all tree nodes via shared ViewModel
    }
}
