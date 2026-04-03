namespace TaskRunnerExtended.ToolWindows;

using System.Runtime.Serialization;

using Ardimedia.VsExtensions.Common.ViewModels;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Shell;
using Microsoft.VisualStudio.Extensibility.UI;
using Microsoft.VisualStudio.ProjectSystem.Query;

using TaskRunnerExtended.Models;
using TaskRunnerExtended.Services;
using TaskRunnerExtended.Services.Discovery;
using TaskRunnerExtended.Services.Execution;

/// <summary>
/// ViewModel for the Task Runner Extended tool window.
/// Inherits solution monitoring and scan lifecycle from <see cref="ToolWindowViewModelBase"/>.
/// </summary>
[DataContract]
public class TaskRunnerToolWindowViewModel : ToolWindowViewModelBase
{
    private string _statusText = "No solution loaded";
    private string _workspaceFolder = string.Empty;
    private readonly ITaskDiscoverer[] _discoverers;
    private readonly TaskRunner _taskRunner;
    private readonly FileWatcherService _fileWatcher;
    private readonly GroupConfigService _groupConfigService = new();

    // Lookup: tree node by task key (Source.FilePath::Label)
    private readonly Dictionary<string, TaskTreeNode> _taskNodeMap = [];

    public TaskRunnerToolWindowViewModel(VisualStudioExtensibility extensibility)
        : base(extensibility)
    {
        // Faster solution detection for sidebar tool window (visible immediately)
        this.InitialDelayMs = 300;
        this.PollIntervalMs = 1000;
        this.DebounceIntervalMs = 2000;
        this.StableReadingsRequired = 2;

        _discoverers =
        [
            new TasksJsonDiscoverer(),
            new TasksVsJsonDiscoverer(),
            new PackageJsonDiscoverer(),
            new CsprojDiscoverer(),
            new LaunchSettingsDiscoverer(),
            new ComposeYmlDiscoverer(),
            // Phase 2 later: GruntfileDiscoverer, GulpfileDiscoverer (require shell out)
        ];

        _taskRunner = new TaskRunner(extensibility);
        _taskRunner.TaskStatusChanged += OnTaskStatusChanged;

        // Subscribe to toolbar actions
        ToolbarActionBus.RefreshRequested += () =>
        {
            StatusText = "Refreshing...";
            _ = Task.Run(async () =>
            {
                try { await OnSolutionOpenedAsync(CancellationToken.None).ConfigureAwait(false); }
                catch { /* ignore */ }
            });
        };
        ToolbarActionBus.StopAllRequested += () =>
        {
            StatusText = "Stopping all tasks...";
            _ = Task.Run(async () =>
            {
                await _taskRunner.StopAllAsync().ConfigureAwait(false);
                // Reset all task node statuses
                foreach (var node in _taskNodeMap.Values)
                {
                    node.Status = Models.TaskStatus.Idle;
                }
                RefreshGroupsInTree();
                StatusText = "All tasks stopped.";
            });
        };

        _fileWatcher = new FileWatcherService(() =>
        {
            // Re-scan when task source files change
            StatusText = "File change detected, rescanning...";
            _ = Task.Run(async () =>
            {
                try
                {
                    await OnSolutionOpenedAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Rescan failed — ignore
                }
            });
        });

        StartTaskCommand = new(async (parameter, ct) =>
        {
            if (parameter is not string taskName) return;

            var node = FindTaskNode(taskName);
            if (node?.Task is null) return;

            try
            {
                if (node.Task.IsCompound)
                {
                    // Compound task: start all dependent tasks
                    await StartCompoundTaskAsync(node).ConfigureAwait(false);
                }
                else
                {
                    node.Status = Models.TaskStatus.Running;
                    StatusText = $"Starting: {node.Task.Label}...";

                    var started = await _taskRunner.StartAsync(node.Task, _workspaceFolder).ConfigureAwait(false);
                    StatusText = started
                        ? $"Running: {node.Task.Label}"
                        : $"Failed to start: {node.Task.Label}";

                    if (!started)
                    {
                        node.Status = Models.TaskStatus.Error;
                    }
                }
            }
            catch (Exception ex)
            {
                node.Status = Models.TaskStatus.Error;
                StatusText = $"Error starting {taskName}: {ex.Message}";
            }
        });

        StopTaskCommand = new(async (parameter, ct) =>
        {
            if (parameter is not string taskName) return;

            var node = FindTaskNode(taskName);
            if (node?.Task is null)
            {
                StatusText = $"No runnable task found: {taskName}";
                return;
            }

            try
            {
                StatusText = $"Stopping: {node.Task.Label}...";
                await _taskRunner.StopAsync(node.Task).ConfigureAwait(false);
                node.Status = Models.TaskStatus.Idle;
                StatusText = $"Stopped: {node.Task.Label}";
            }
            catch (Exception ex)
            {
                StatusText = $"Error stopping {taskName}: {ex.Message}";
            }
        });

        SelectNodeCommand = new((parameter, ct) =>
        {
            // Deselect previous
            if (_selectedNode is not null)
            {
                _selectedNode.IsNodeSelected = false;
            }

            // Find and select new node by name
            if (parameter is string name)
            {
                _selectedNode = FindAllNodes().FirstOrDefault(n => n.Name == name);
                if (_selectedNode is not null)
                {
                    _selectedNode.IsNodeSelected = true;
                }
            }

            return Task.CompletedTask;
        });

        // "Add to Group..." — input prompt with first existing group as default
        AddToGroupCommand = new(async (parameter, ct) =>
        {
            if (parameter is not string taskName || string.IsNullOrEmpty(_workspaceFolder)) return;

            var node = FindTaskNode(taskName);
            if (node?.Task is null) return;

            var existingGroups = _groupConfigService.GetGroupNames(_workspaceFolder);
            var defaultName = existingGroups.Count > 0 ? existingGroups[0] : "Development";

            var groupName = await this.Extensibility.Shell().ShowPromptAsync(
                $"Add '{taskName}' to group:",
                InputPromptOptions.Default with { DefaultText = defaultName },
                ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(groupName)) return;

            _groupConfigService.AddTaskToGroup(_workspaceFolder, groupName, new Models.TaskGroupEntry
            {
                Source = node.Task.Source.DisplayName,
                Task = node.Task.Label,
            });

            StatusText = $"Added '{taskName}' to group '{groupName}'";
            RefreshGroupsInTree();
        });

        // Start all tasks in a group
        StartGroupCommand = new(async (parameter, ct) =>
        {
            if (parameter is not string groupName || string.IsNullOrEmpty(_workspaceFolder)) return;

            var groups = _groupConfigService.LoadGroups(_workspaceFolder);
            var group = groups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
            if (group is null) return;

            StatusText = $"Starting group: {groupName}...";

            foreach (var entry in group.Tasks.OrderBy(t => t.Order))
            {
                var taskNode = FindTaskNode(entry.Task);
                if (taskNode?.Task is null) continue;

                taskNode.Status = Models.TaskStatus.Running;

                if (entry.StartOrder == "parallel")
                {
                    _ = _taskRunner.StartAsync(taskNode.Task, _workspaceFolder);
                }
                else
                {
                    var started = await _taskRunner.StartAsync(taskNode.Task, _workspaceFolder).ConfigureAwait(false);
                    if (!started)
                    {
                        taskNode.Status = Models.TaskStatus.Error;
                        continue;
                    }

                    if (taskNode.Task.TaskType == TaskType.Normal)
                    {
                        while (_taskRunner.IsRunning(taskNode.Task))
                        {
                            await Task.Delay(200).ConfigureAwait(false);
                        }
                    }
                }
            }

            StatusText = $"Running group: {groupName}";
            RefreshGroupsInTree();
        });

        // Stop all tasks in a group
        StopGroupCommand = new(async (parameter, ct) =>
        {
            if (parameter is not string groupName || string.IsNullOrEmpty(_workspaceFolder)) return;

            var groups = _groupConfigService.LoadGroups(_workspaceFolder);
            var group = groups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
            if (group is null) return;

            StatusText = $"Stopping group: {groupName}...";

            foreach (var entry in group.Tasks)
            {
                var taskNode = FindTaskNode(entry.Task);
                if (taskNode?.Task is null) continue;

                await _taskRunner.StopAsync(taskNode.Task).ConfigureAwait(false);
                taskNode.Status = Models.TaskStatus.Idle;
            }

            StatusText = $"Stopped group: {groupName}";
            RefreshGroupsInTree();
        });

        // Create a new group via input prompt
        CreateGroupCommand = new(async (parameter, ct) =>
        {
            if (string.IsNullOrEmpty(_workspaceFolder)) return;

            var name = await PromptForGroupNameAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(name)) return;

            _groupConfigService.SaveGroup(_workspaceFolder, new Models.TaskGroup { Name = name });
            StatusText = $"Created group: {name}";
            RefreshGroupsInTree();
        });

        // Delete a group with confirmation
        DeleteGroupCommand = new(async (parameter, ct) =>
        {
            if (parameter is not string groupName || string.IsNullOrEmpty(_workspaceFolder)) return;

            var confirmed = await this.Extensibility.Shell().ShowPromptAsync(
                $"Delete group '{groupName}'?",
                PromptOptions.OKCancel,
                ct).ConfigureAwait(false);

            if (!confirmed) return;

            _groupConfigService.DeleteGroup(_workspaceFolder, groupName);
            StatusText = $"Deleted group: {groupName}";
            RefreshGroupsInTree();
        });

        // Rename a group via input prompt
        RenameGroupCommand = new(async (parameter, ct) =>
        {
            if (parameter is not string oldName || string.IsNullOrEmpty(_workspaceFolder)) return;

            var newName = await this.Extensibility.Shell().ShowPromptAsync(
                $"Rename group '{oldName}' to:",
                InputPromptOptions.Default with { DefaultText = oldName },
                ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(newName) || newName == oldName) return;

            var groups = _groupConfigService.LoadGroups(_workspaceFolder);
            var group = groups.FirstOrDefault(g => g.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));
            if (group is null) return;

            _groupConfigService.DeleteGroup(_workspaceFolder, oldName);
            group.Name = newName;
            _groupConfigService.SaveGroup(_workspaceFolder, group);

            StatusText = $"Renamed group: '{oldName}' → '{newName}'";
            RefreshGroupsInTree();
        });

        // Remove a task from a group — parameter format: "groupName|taskLabel"
        RemoveFromGroupCommand = new((parameter, ct) =>
        {
            if (parameter is not string param || string.IsNullOrEmpty(_workspaceFolder)) return Task.CompletedTask;

            var parts = param.Split('|', 2);
            if (parts.Length != 2) return Task.CompletedTask;

            var groupName = parts[0];
            var taskLabel = parts[1];

            // Find the source for this task
            var taskNode = FindTaskNode(taskLabel);
            var source = taskNode?.Task?.Source.DisplayName ?? string.Empty;

            _groupConfigService.RemoveTaskFromGroup(_workspaceFolder, groupName, source, taskLabel);
            StatusText = $"Removed '{taskLabel}' from group '{groupName}'";
            RefreshGroupsInTree();
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
    public ObservableList<TaskTreeNode> TreeItems { get; } = [];

    [DataMember]
    public AsyncCommand StartTaskCommand { get; }

    [DataMember]
    public AsyncCommand StopTaskCommand { get; }

    [DataMember]
    public AsyncCommand SelectNodeCommand { get; }

    [DataMember]
    public AsyncCommand AddToGroupCommand { get; }

    [DataMember]
    public AsyncCommand StartGroupCommand { get; }

    [DataMember]
    public AsyncCommand StopGroupCommand { get; }

    [DataMember]
    public AsyncCommand CreateGroupCommand { get; }

    [DataMember]
    public AsyncCommand DeleteGroupCommand { get; }

    [DataMember]
    public AsyncCommand RenameGroupCommand { get; }

    [DataMember]
    public AsyncCommand RemoveFromGroupCommand { get; }

    private TaskTreeNode? _selectedNode;

    /// <inheritdoc />
    protected override async Task OnSolutionOpenedAsync(CancellationToken cancellationToken)
    {
        // Check if a solution is actually loaded (base class may call this before solution is ready)
        var fingerprint = await GetSolutionFingerprintAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(fingerprint))
        {
            StatusText = "Waiting for a solution to be opened...";
            return;
        }

        StatusText = "Scanning for task sources...";
        TreeItems.Clear();
        _taskNodeMap.Clear();

        try
        {
            _workspaceFolder = await GetSolutionDirectoryAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(_workspaceFolder))
            {
                StatusText = "Waiting for a solution to be opened...";
                return;
            }

            var configFilesRoot = new TaskTreeNode("Available Configuration Files (Tasks)", TreeIcons.ConfigFiles)
            { SelectCommand = SelectNodeCommand, FontWeight = "Bold" };
            var projectDirs = await GetProjectDirectoriesAsync(cancellationToken).ConfigureAwait(false);

            // Discover tasks from solution directory
            await DiscoverInDirectoryAsync(_workspaceFolder, configFilesRoot, cancellationToken).ConfigureAwait(false);

            // Discover tasks from each project directory
            foreach (var projectDir in projectDirs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.Equals(projectDir, _workspaceFolder, StringComparison.OrdinalIgnoreCase))
                {
                    await DiscoverInDirectoryAsync(projectDir, configFilesRoot, cancellationToken).ConfigureAwait(false);
                }
            }

            // Discover tasks from parent directories (up to maxParentDepth)
            const int maxParentDepth = 3;
            var parentDir = Directory.GetParent(_workspaceFolder)?.FullName;
            for (int depth = 0; depth < maxParentDepth && parentDir is not null; depth++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var parentNode = new TaskTreeNode($"[Parent: {parentDir}]", TreeIcons.Folder)
                { SelectCommand = SelectNodeCommand };

                await DiscoverInDirectoryAsync(parentDir, parentNode, cancellationToken).ConfigureAwait(false);

                if (parentNode.Children.Count > 0)
                {
                    configFilesRoot.Children.Add(parentNode);
                }

                parentDir = Directory.GetParent(parentDir)?.FullName;
            }

            if (configFilesRoot.Children.Count > 0)
            {
                TreeItems.Add(configFilesRoot);
            }
            else
            {
                // Empty state: no tasks found
                var emptyNode = new TaskTreeNode("No task sources found", TreeIcons.ParseError)
                { SelectCommand = SelectNodeCommand };
                emptyNode.Children.Add(new TaskTreeNode("Create a .vscode/tasks.json file to define tasks", "")
                { SelectCommand = SelectNodeCommand });
                emptyNode.Children.Add(new TaskTreeNode("Or add npm scripts to package.json", "")
                { SelectCommand = SelectNodeCommand });
                emptyNode.Children.Add(new TaskTreeNode("Or add MSBuild <Exec> targets to your .csproj", "")
                { SelectCommand = SelectNodeCommand });
                TreeItems.Add(emptyNode);
            }

            // Run Groups
            BuildGroupsTree();

            var taskCount = CountTasks(configFilesRoot);
            StatusText = taskCount > 0
                ? $"Found {taskCount} task(s) from {configFilesRoot.Children.Count} source(s)."
                : "No tasks found. Add task source files to get started.";

            // Start watching for file changes
            var watchDirs = new List<string> { _workspaceFolder };
            watchDirs.AddRange(projectDirs);
            _fileWatcher.Watch(watchDirs);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error during scan: {ex.Message}";
        }
    }

    /// <inheritdoc />
    protected override void OnSolutionClosed()
    {
        _fileWatcher.Stop();
        _ = _taskRunner.StopAllAsync();
        TreeItems.Clear();
        _taskNodeMap.Clear();
        _workspaceFolder = string.Empty;
        StatusText = "No solution loaded";
    }

    private void OnTaskStatusChanged(TaskItem task, Models.TaskStatus status)
    {
        var key = $"{task.Source.FilePath}::{task.Label}";
        if (_taskNodeMap.TryGetValue(key, out var node))
        {
            node.Status = status;
        }

        // Refresh groups to update entry icons and group CanStart/CanStop
        RefreshGroupsInTree();
    }

    private async Task<string?> PromptForGroupNameAsync(CancellationToken ct)
    {
        return await this.Extensibility.Shell().ShowPromptAsync(
            "Enter name for the new group:",
            InputPromptOptions.Default with { DefaultText = "Development" },
            ct).ConfigureAwait(false);
    }

    private void BuildGroupsTree()
    {
        // Remove existing groups root if present
        var existingGroupsRoot = TreeItems.FirstOrDefault(n => n.Name == "Run Groups");
        if (existingGroupsRoot is not null)
        {
            TreeItems.Remove(existingGroupsRoot);
        }

        var groupsRoot = new TaskTreeNode("Run Groups", TreeIcons.RunGroups)
        { SelectCommand = SelectNodeCommand, FontWeight = "Bold" };

        if (!string.IsNullOrEmpty(_workspaceFolder))
        {
            var groups = _groupConfigService.LoadGroups(_workspaceFolder);

            foreach (var group in groups)
            {
                var groupNode = new TaskTreeNode(group.Name, TreeIcons.Group)
                {
                    CanStart = true,
                    CanStop = true,
                    SelectCommand = SelectNodeCommand,
                    StartCommand = StartGroupCommand,
                    StopCommand = StopGroupCommand,
                    DeleteCommand = DeleteGroupCommand,
                    RenameCommand = RenameGroupCommand,
                };

                var hasRunningTasks = false;
                foreach (var entry in group.Tasks.OrderBy(t => t.Order))
                {
                    var taskNode = FindTaskNode(entry.Task);
                    var isRunning = taskNode?.Task is not null && _taskRunner.IsRunning(taskNode.Task);
                    if (isRunning) hasRunningTasks = true;

                    var icon = taskNode is null ? TreeIcons.ParseError
                        : isRunning ? TreeIcons.TaskRunning
                        : TreeIcons.TaskIdle;

                    var entryNode = new TaskTreeNode(entry.Task, icon)
                    {
                        Metadata = taskNode is null ? " (not found)" : $" ({entry.StartOrder})",
                        SelectCommand = SelectNodeCommand,
                        RemoveFromGroupCommand = RemoveFromGroupCommand,
                        RemoveFromGroupParam = $"{group.Name}|{entry.Task}",
                    };
                    groupNode.Children.Add(entryNode);
                }

                groupNode.CanStart = !hasRunningTasks;
                groupNode.CanStop = hasRunningTasks;

                groupsRoot.Children.Add(groupNode);
            }
        }

        // "New Group..." node
        var newGroupNode = new TaskTreeNode("+ New Group...", TreeIcons.Group)
        {
            StartCommand = CreateGroupCommand,
            SelectCommand = SelectNodeCommand,
        };
        groupsRoot.Children.Add(newGroupNode);

        TreeItems.Add(groupsRoot);
    }

    private void RefreshGroupsInTree()
    {
        BuildGroupsTree();
    }

    private TaskTreeNode? FindTaskNode(string name)
    {
        return _taskNodeMap.Values.FirstOrDefault(n => n.Name == name);
    }

    private async Task StartCompoundTaskAsync(TaskTreeNode compoundNode)
    {
        var task = compoundNode.Task!;
        var isParallel = task.DependsOrder.Equals("parallel", StringComparison.OrdinalIgnoreCase);

        StatusText = $"Starting compound task: {task.Label} ({task.DependsOn.Length} dependencies, {task.DependsOrder})...";
        compoundNode.Status = Models.TaskStatus.Running;

        foreach (var depLabel in task.DependsOn)
        {
            var depNode = FindTaskNode(depLabel);
            if (depNode?.Task is null)
            {
                StatusText = $"Dependency not found: {depLabel}";
                continue;
            }

            depNode.Status = Models.TaskStatus.Running;

            if (isParallel)
            {
                // Fire and forget — start all in parallel
                _ = _taskRunner.StartAsync(depNode.Task, _workspaceFolder);
            }
            else
            {
                // Sequential: start and wait for normal tasks, start immediately for background tasks
                var started = await _taskRunner.StartAsync(depNode.Task, _workspaceFolder).ConfigureAwait(false);
                if (!started)
                {
                    depNode.Status = Models.TaskStatus.Error;
                    StatusText = $"Failed to start dependency: {depLabel}";
                    continue;
                }

                // For normal tasks in sequence, wait for completion before starting next
                if (depNode.Task.TaskType == TaskType.Normal)
                {
                    // Wait until the task finishes
                    while (_taskRunner.IsRunning(depNode.Task))
                    {
                        await Task.Delay(200).ConfigureAwait(false);
                    }

                    var exitCode = _taskRunner.GetExitCode(depNode.Task);
                    if (exitCode is not null and not 0)
                    {
                        depNode.Status = Models.TaskStatus.Error;
                        StatusText = $"Dependency failed: {depLabel} (exit code {exitCode})";
                        compoundNode.Status = Models.TaskStatus.Error;
                        return; // Stop sequence on failure
                    }
                }
                // Background tasks in sequence: start immediately, don't wait
            }
        }

        StatusText = $"Running: {task.Label}";
    }

    private IEnumerable<TaskTreeNode> FindAllNodes()
    {
        return EnumerateNodes(TreeItems);

        static IEnumerable<TaskTreeNode> EnumerateNodes(IEnumerable<TaskTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                yield return node;
                foreach (var child in EnumerateNodes(node.Children))
                {
                    yield return child;
                }
            }
        }
    }

    private async Task DiscoverInDirectoryAsync(
        string directory, TaskTreeNode parentNode, CancellationToken cancellationToken)
    {
        foreach (var discoverer in _discoverers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tasks = await discoverer.DiscoverAsync(directory, cancellationToken).ConfigureAwait(false);
            if (tasks.Count == 0) continue;

            var sourceGroups = tasks.GroupBy(t => t.Source.DisplayName);
            foreach (var group in sourceGroups)
            {
                var firstTask = group.First();
                var sourceIcon = TreeIcons.ForSourceKind(firstTask.Source.Kind);
                var sourceNode = new TaskTreeNode(group.Key, sourceIcon)
                { SelectCommand = SelectNodeCommand };

                foreach (var task in group)
                {
                    if (task.IsError)
                    {
                        var errorNode = new TaskTreeNode(task.Label, TreeIcons.ParseError)
                        { SelectCommand = SelectNodeCommand };
                        sourceNode.Children.Add(errorNode);
                        continue;
                    }

                    var metadata = task.Metadata is not null ? $" ({task.Metadata})" : string.Empty;
                    var taskNode = new TaskTreeNode(task.Label, task)
                    {
                        Metadata = metadata,
                        StartCommand = StartTaskCommand,
                        StopCommand = task.IsCompound ? null : StopTaskCommand,
                        AddToGroupCommand = AddToGroupCommand,
                        SelectCommand = SelectNodeCommand,
                    };
                    sourceNode.Children.Add(taskNode);

                    // Register in lookup map
                    var key = $"{task.Source.FilePath}::{task.Label}";
                    _taskNodeMap[key] = taskNode;
                }

                parentNode.Children.Add(sourceNode);
            }
        }
    }

    private async Task<string> GetSolutionDirectoryAsync(CancellationToken cancellationToken)
    {
        var projects = await this.Extensibility.Workspaces().QueryProjectsAsync(
            q => q.With(p => p.Path),
            cancellationToken).ConfigureAwait(false);

        var directories = projects
            .Select(p => p.Path)
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => Path.GetDirectoryName(p)!)
            .Distinct()
            .ToList();

        return FindCommonRoot(directories);
    }

    private async Task<List<string>> GetProjectDirectoriesAsync(CancellationToken cancellationToken)
    {
        var projects = await this.Extensibility.Workspaces().QueryProjectsAsync(
            q => q.With(p => p.Path),
            cancellationToken).ConfigureAwait(false);

        return projects
            .Select(p => p.Path)
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => Path.GetDirectoryName(p)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FindCommonRoot(List<string> directories)
    {
        if (directories.Count == 0) return string.Empty;
        if (directories.Count == 1) return directories[0];

        var first = directories[0].Split(Path.DirectorySeparatorChar);
        var commonLength = first.Length;

        foreach (var dir in directories.Skip(1))
        {
            var parts = dir.Split(Path.DirectorySeparatorChar);
            commonLength = Math.Min(commonLength, parts.Length);

            for (int i = 0; i < commonLength; i++)
            {
                if (!string.Equals(first[i], parts[i], StringComparison.OrdinalIgnoreCase))
                {
                    commonLength = i;
                    break;
                }
            }
        }

        return string.Join(Path.DirectorySeparatorChar, first.Take(commonLength));
    }

    private static int CountTasks(TaskTreeNode node)
    {
        var count = node.Task is not null ? 1 : 0;
        foreach (var child in node.Children)
        {
            count += CountTasks(child);
        }

        return count;
    }
}
