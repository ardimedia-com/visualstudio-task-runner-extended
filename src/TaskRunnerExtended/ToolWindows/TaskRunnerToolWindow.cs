namespace TaskRunnerExtended.ToolWindows;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.ToolWindows;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;

/// <summary>
/// Tool window provider for the Task Runner Extended panel.
/// </summary>
[VisualStudioContribution]
public class TaskRunnerToolWindow : ToolWindow
{
    private TaskRunnerToolWindowViewModel? _viewModel;

    public TaskRunnerToolWindow(VisualStudioExtensibility extensibility)
        : base(extensibility)
    {
        this.Title = "Task Runner Extended";
    }

    /// <inheritdoc />
    public override ToolWindowConfiguration ToolWindowConfiguration => new()
    {
        // TODO: Phase 1 spike will determine if DockedTo(SolutionExplorerGuid) works for sidebar placement.
        // Fallback: ToolWindowPlacement.DocumentWell
        Placement = ToolWindowPlacement.DocumentWell,
    };

    /// <inheritdoc />
    public override async Task<IRemoteUserControl> GetContentAsync(CancellationToken cancellationToken)
    {
        _viewModel = new TaskRunnerToolWindowViewModel(this.Extensibility);
        _viewModel.Initialize();

        return new TaskRunnerToolWindowControl(_viewModel);
    }

    /// <inheritdoc />
    public override Task OnHideAsync(CancellationToken cancellationToken)
    {
        _viewModel?.Dispose();
        return base.OnHideAsync(cancellationToken);
    }
}
