namespace TaskRunnerExtended.ToolWindows;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.ToolWindows;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;

using TaskRunnerExtended.Commands;

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

    /// <summary>
    /// Toolbar definition for the tool window header.
    /// </summary>
    [VisualStudioContribution]
    public static ToolbarConfiguration Toolbar => new("%TaskRunnerExtended.Toolbar.DisplayName%")
    {
        Children =
        [
            ToolbarChild.Command<ShowTasksCommand>(),
            ToolbarChild.Command<ShowBackgroundCommand>(),
            ToolbarChild.Command<ShowFeedbackCommand>(),
            ToolbarChild.Separator,
            ToolbarChild.Command<RefreshCommand>(),
            ToolbarChild.Separator,
            ToolbarChild.Command<StopAllCommand>(),
            ToolbarChild.Separator,
            ToolbarChild.Command<CollapseAllCommand>(),
        ],
    };

    /// <inheritdoc />
    public override ToolWindowConfiguration ToolWindowConfiguration => new()
    {
        Placement = ToolWindowPlacement.DockedTo(SolutionExplorerGuid),
        DockDirection = Dock.None,
        Toolbar = new(Toolbar),
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
