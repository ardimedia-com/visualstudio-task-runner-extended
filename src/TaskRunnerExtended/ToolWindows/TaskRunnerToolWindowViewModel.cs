namespace TaskRunnerExtended.ToolWindows;

using System.Runtime.Serialization;

using Ardimedia.VsExtensions.Common.ViewModels;

using Microsoft.VisualStudio.Extensibility;

/// <summary>
/// ViewModel for the Task Runner Extended tool window.
/// Inherits solution monitoring and scan lifecycle from <see cref="ToolWindowViewModelBase"/>.
/// </summary>
[DataContract]
public class TaskRunnerToolWindowViewModel : ToolWindowViewModelBase
{
    private string _statusText = "No solution loaded";

    public TaskRunnerToolWindowViewModel(VisualStudioExtensibility extensibility)
        : base(extensibility)
    {
    }

    [DataMember]
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <inheritdoc />
    protected override async Task OnSolutionOpenedAsync(CancellationToken cancellationToken)
    {
        // TODO: Phase 1 Step 3 — Discover task source files and populate the tree
        this.StatusText = "Scanning for task sources...";
        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        this.StatusText = "Ready";
    }

    /// <inheritdoc />
    protected override void OnSolutionClosed()
    {
        // TODO: Clear discovered tasks and running processes
        this.StatusText = "No solution loaded";
    }
}
