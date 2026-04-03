namespace TaskRunnerExtended.ToolWindows;

using System.Runtime.Serialization;

using Ardimedia.VsExtensions.Common.ViewModels;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.UI;

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
        // Spike: populate sample tree data to test TreeView + ContextMenu in Remote UI
        SpikeTreeItems.Add(new SpikeTreeNode("Available Configuration Files (Tasks)")
        {
            Children =
            {
                new SpikeTreeNode(".vscode/tasks.json")
                {
                    Children =
                    {
                        new SpikeTreeNode("watchcss") { Icon = "o" },
                        new SpikeTreeNode("dotnet-watch") { Icon = ">" },
                        new SpikeTreeNode("dev (compound)") { Icon = "*" },
                    },
                },
                new SpikeTreeNode("compose.yml")
                {
                    Children =
                    {
                        new SpikeTreeNode("docker: db") { Icon = "o" },
                        new SpikeTreeNode("docker: redis") { Icon = ">" },
                    },
                },
                new SpikeTreeNode("package.json")
                {
                    Children =
                    {
                        new SpikeTreeNode("npm: buildcss") { Icon = "o" },
                        new SpikeTreeNode("npm: watchcss") { Icon = ">" },
                    },
                },
            },
        });

        SpikeTreeItems.Add(new SpikeTreeNode("Run Groups")
        {
            Children =
            {
                new SpikeTreeNode("Development (3 running)") { Icon = ">>" },
                new SpikeTreeNode("Build Production") { Icon = "o" },
            },
        });

        SpikeContextMenuCommand = new((parameter, ct) =>
        {
            StatusText = $"Context menu clicked: {parameter}";
            return Task.CompletedTask;
        });
    }

    [DataMember]
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    [DataMember]
    public ObservableList<SpikeTreeNode> SpikeTreeItems { get; } = [];

    [DataMember]
    public AsyncCommand SpikeContextMenuCommand { get; }

    /// <inheritdoc />
    protected override async Task OnSolutionOpenedAsync(CancellationToken cancellationToken)
    {
        this.StatusText = "Solution opened — spike data is static for now.";
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void OnSolutionClosed()
    {
        this.StatusText = "No solution loaded";
    }
}

/// <summary>
/// Spike: simple tree node model to test TreeView + HierarchicalDataTemplate in Remote UI.
/// </summary>
[DataContract]
public class SpikeTreeNode : NotifyPropertyChangedObject
{
    public SpikeTreeNode(string name)
    {
        Name = name;
    }

    [DataMember]
    public string Name { get; set; } = string.Empty;

    [DataMember]
    public string Icon { get; set; } = string.Empty;

    [DataMember]
    public ObservableList<SpikeTreeNode> Children { get; } = [];
}
