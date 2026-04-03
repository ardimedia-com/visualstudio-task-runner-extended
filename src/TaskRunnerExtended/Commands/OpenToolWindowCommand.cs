namespace TaskRunnerExtended.Commands;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;

using TaskRunnerExtended.ToolWindows;

/// <summary>
/// Command that opens the Task Runner Extended tool window.
/// Placed in the Tools menu.
/// </summary>
[VisualStudioContribution]
public class OpenToolWindowCommand : Command
{
    /// <inheritdoc />
    #pragma warning disable CEE0027 // String not localized
    public override CommandConfiguration CommandConfiguration => new("Task Runner Extended")
    {
        TooltipText = "Open the Task Runner Extended tool window",
        Icon = new(ImageMoniker.Custom("TaskRunnerExtended"), IconSettings.IconAndText),
        Placements = [CommandPlacement.KnownPlacements.ToolsMenu],
    };

    public OpenToolWindowCommand(VisualStudioExtensibility extensibility)
        : base(extensibility)
    {
    }

    /// <inheritdoc />
    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        await this.Extensibility.Shell().ShowToolWindowAsync<TaskRunnerToolWindow>(
            activate: true, cancellationToken);
    }
}
