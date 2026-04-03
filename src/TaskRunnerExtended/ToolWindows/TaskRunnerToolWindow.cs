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

    // Solution Explorer GUID (standard VS tool window)
    private static readonly Guid SolutionExplorerGuid = new("3AE79031-E1BC-11D0-8F78-00A0C9110057");

    /// <inheritdoc />
    public override ToolWindowConfiguration ToolWindowConfiguration => new()
    {
        // Spike 3: Test sidebar placement next to Solution Explorer
        Placement = ToolWindowPlacement.DockedTo(SolutionExplorerGuid),
        DockDirection = Dock.None,
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
