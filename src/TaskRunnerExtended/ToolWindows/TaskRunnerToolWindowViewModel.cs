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
    private string _activeTab = "Tasks";
    private bool _showTasks = true;
    private bool _showBackground;
    private bool _showFeedback;
    private string _feedbackTitle = "BUG: ";
    private string _feedbackBody = string.Empty;
    private string _feedbackType = "Bug";
    private string _feedbackStatus = string.Empty;
    private string _extensionVersion = string.Empty;
    private readonly ITaskDiscoverer[] _discoverers;
    private readonly TaskRunner _taskRunner;
    private readonly FileWatcherService _fileWatcher;
    private readonly GroupConfigService _groupConfigService = new();

    // Lookup: tree node by task key (Source.FilePath::Label)
    private readonly Dictionary<string, TaskTreeNode> _taskNodeMap = [];

    // Group entry nodes keyed by task label — updated when task status changes
    private readonly Dictionary<string, List<TaskTreeNode>> _groupEntryNodes = [];

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

        // Subscribe to toolbar actions (named methods for cleanup in Dispose)
        ToolbarActionBus.RefreshRequested += OnRefreshRequested;
        ToolbarActionBus.StopAllRequested += OnStopAllRequested;
        ToolbarActionBus.CollapseAllRequested += OnCollapseAllRequested;
        ToolbarActionBus.TabChanged += OnTabChanged;

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
                    UpdateGroupEntryStatus(node.Task.Label, Models.TaskStatus.Running);
                    StatusText = $"Starting: {node.Task.Label}...";

                    var started = await _taskRunner.StartAsync(node.Task, _workspaceFolder).ConfigureAwait(false);
                    StatusText = started
                        ? $"Running: {node.Task.Label}"
                        : $"Failed to start: {node.Task.Label}";

                    if (!started)
                    {
                        node.Status = Models.TaskStatus.Error;
                        UpdateGroupEntryStatus(node.Task.Label, Models.TaskStatus.Error);
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
                UpdateGroupEntryStatus(node.Task.Label, Models.TaskStatus.Idle);
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

            UpdateDetailsPane(_selectedNode);

            return Task.CompletedTask;
        });

        // "Add to Group..." — input prompt with first existing group as default
        AddToGroupCommand = new(async (parameter, ct) =>
        {
            if (parameter is not string taskName || string.IsNullOrEmpty(_workspaceFolder)) return;

            var node = FindTaskNode(taskName);
            if (node?.Task is null) return;

            // Prompt for shared vs local: OK = shared (commited), Cancel = local (not commited)
            var toShared = await this.Extensibility.Shell().ShowPromptAsync(
                "Save to shared file (commited to git)?\nOK = Shared, Cancel = Local (not commited)",
                PromptOptions.OKCancel,
                ct).ConfigureAwait(false);

            // Get existing groups from the selected file
            var existingGroups = toShared
                ? _groupConfigService.LoadSharedGroups(_workspaceFolder)
                : _groupConfigService.LoadLocalGroups(_workspaceFolder);
            var defaultName = existingGroups.Count > 0 ? existingGroups[0].Name : "Development";

            var groupName = await this.Extensibility.Shell().ShowPromptAsync(
                $"Add '{taskName}' to group:",
                InputPromptOptions.Default with { DefaultText = defaultName },
                ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(groupName)) return;

            _groupConfigService.AddTaskToGroup(_workspaceFolder, groupName, new Models.TaskGroupEntry
            {
                Source = node.Task.Source.DisplayName,
                Task = node.Task.Label,
            }, toShared);

            StatusText = $"Added '{taskName}' to group '{groupName}' ({(toShared ? "shared" : "local")})";
            RefreshGroupsInTree();
        });

        // Start all tasks in a group
        StartGroupCommand = new(async (parameter, ct) =>
        {
            if (parameter is not string param || string.IsNullOrEmpty(_workspaceFolder)) return;

            var (groupName, isShared) = ParseGroupParam(param);
            var groups = isShared
                ? _groupConfigService.LoadSharedGroups(_workspaceFolder)
                : _groupConfigService.LoadLocalGroups(_workspaceFolder);
            var group = groups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
            if (group is null) return;

            StatusText = $"Starting group: {groupName}...";

            foreach (var entry in group.Tasks.OrderBy(t => t.Order))
            {
                var taskNode = FindTaskNode(entry.Task);
                if (taskNode?.Task is null) continue;

                taskNode.Status = Models.TaskStatus.Running;
                UpdateGroupEntryStatus(entry.Task, Models.TaskStatus.Running);

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
                        UpdateGroupEntryStatus(entry.Task, Models.TaskStatus.Error);
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
            if (parameter is not string param || string.IsNullOrEmpty(_workspaceFolder)) return;

            var (groupName, isShared) = ParseGroupParam(param);
            var groups = isShared
                ? _groupConfigService.LoadSharedGroups(_workspaceFolder)
                : _groupConfigService.LoadLocalGroups(_workspaceFolder);
            var group = groups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
            if (group is null) return;

            StatusText = $"Stopping group: {groupName}...";

            foreach (var entry in group.Tasks)
            {
                var taskNode = FindTaskNode(entry.Task);
                if (taskNode?.Task is null) continue;

                await _taskRunner.StopAsync(taskNode.Task).ConfigureAwait(false);
                taskNode.Status = Models.TaskStatus.Idle;
                UpdateGroupEntryStatus(entry.Task, Models.TaskStatus.Idle);
            }

            StatusText = $"Stopped group: {groupName}";
            RefreshGroupsInTree();
        });

        // Delete a group with confirmation
        DeleteGroupCommand = new(async (parameter, ct) =>
        {
            if (parameter is not string param || string.IsNullOrEmpty(_workspaceFolder)) return;

            var (groupName, isShared) = ParseGroupParam(param);

            var confirmed = await this.Extensibility.Shell().ShowPromptAsync(
                $"Delete group '{groupName}'?",
                PromptOptions.OKCancel,
                ct).ConfigureAwait(false);

            if (!confirmed) return;

            _groupConfigService.DeleteGroup(_workspaceFolder, groupName, isShared);
            StatusText = $"Deleted group: {groupName}";
            RefreshGroupsInTree();
        });

        // Rename a group via input prompt
        RenameGroupCommand = new(async (parameter, ct) =>
        {
            if (parameter is not string param || string.IsNullOrEmpty(_workspaceFolder)) return;

            var (oldName, isShared) = ParseGroupParam(param);

            var newName = await this.Extensibility.Shell().ShowPromptAsync(
                $"Rename group '{oldName}' to:",
                InputPromptOptions.Default with { DefaultText = oldName },
                ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(newName) || newName == oldName) return;

            var groups = isShared
                ? _groupConfigService.LoadSharedGroups(_workspaceFolder)
                : _groupConfigService.LoadLocalGroups(_workspaceFolder);
            var group = groups.FirstOrDefault(g => g.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));
            if (group is null) return;

            _groupConfigService.DeleteGroup(_workspaceFolder, oldName, isShared);
            group.Name = newName;
            _groupConfigService.SaveGroup(_workspaceFolder, group, isShared);

            StatusText = $"Renamed group: '{oldName}' → '{newName}'";
            RefreshGroupsInTree();
        });

        // Remove a task from a group — parameter format: "shared:groupName|taskLabel" or "local:groupName|taskLabel"
        RemoveFromGroupCommand = new((parameter, ct) =>
        {
            if (parameter is not string param || string.IsNullOrEmpty(_workspaceFolder)) return Task.CompletedTask;

            var parts = param.Split('|', 2);
            if (parts.Length != 2) return Task.CompletedTask;

            var (groupName, isShared) = ParseGroupParam(parts[0]);
            var taskLabel = parts[1];

            // Find the source for this task
            var taskNode = FindTaskNode(taskLabel);
            var source = taskNode?.Task?.Source.DisplayName ?? string.Empty;

            _groupConfigService.RemoveTaskFromGroup(_workspaceFolder, groupName, source, taskLabel, isShared);
            StatusText = $"Removed '{taskLabel}' from group '{groupName}'";
            RefreshGroupsInTree();
            return Task.CompletedTask;
        });

        // Add a new empty group to shared or local file
        AddGroupCommand = new(async (parameter, ct) =>
        {
            if (parameter is not string prefix || string.IsNullOrEmpty(_workspaceFolder)) return;

            var isShared = prefix == "shared";
            var groupName = await this.Extensibility.Shell().ShowPromptAsync(
                "New group name:",
                InputPromptOptions.Default with { DefaultText = "Development" },
                ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(groupName)) return;

            _groupConfigService.SaveGroup(_workspaceFolder, new Models.TaskGroup { Name = groupName }, isShared);
            StatusText = $"Created group: {groupName} ({(isShared ? "shared" : "local")})";
            RefreshGroupsInTree();
        });

        // Start all tasks from a source file
        StartAllInSourceCommand = new(async (parameter, ct) =>
        {
            if (parameter is not string sourceName || string.IsNullOrEmpty(_workspaceFolder)) return;

            var nodes = _taskNodeMap.Values.Where(n => n.Name != sourceName && n.Task is not null).ToList();
            // Find children of the source node by matching task source display name
            var sourceNodes = _taskNodeMap.Values
                .Where(n => n.Task is not null && GetRelativeSourceName(n.Task.Source.FilePath) == sourceName)
                .ToList();

            if (sourceNodes.Count == 0) return;

            StatusText = $"Starting all tasks from {sourceName}...";
            foreach (var node in sourceNodes)
            {
                if (node.Task!.IsCompound || node.Task.IsError) continue;
                node.Status = Models.TaskStatus.Running;
                UpdateGroupEntryStatus(node.Task.Label, Models.TaskStatus.Running);
                _ = _taskRunner.StartAsync(node.Task, _workspaceFolder);
            }
            StatusText = $"Started {sourceNodes.Count} task(s) from {sourceName}";
        });

        // Stop all tasks from a source file
        StopAllInSourceCommand = new(async (parameter, ct) =>
        {
            if (parameter is not string sourceName || string.IsNullOrEmpty(_workspaceFolder)) return;

            var sourceNodes = _taskNodeMap.Values
                .Where(n => n.Task is not null && GetRelativeSourceName(n.Task.Source.FilePath) == sourceName)
                .ToList();

            if (sourceNodes.Count == 0) return;

            StatusText = $"Stopping all tasks from {sourceName}...";
            foreach (var node in sourceNodes)
            {
                if (node.Task is null || !_taskRunner.IsRunning(node.Task)) continue;
                await _taskRunner.StopAsync(node.Task).ConfigureAwait(false);
                node.Status = Models.TaskStatus.Idle;
                UpdateGroupEntryStatus(node.Task.Label, Models.TaskStatus.Idle);
            }
            StatusText = $"Stopped tasks from {sourceName}";
        });

        // Prefill feedback body with version info
        var version = typeof(TaskRunnerToolWindowViewModel).Assembly.GetName().Version;
        var versionText = version is not null ? $"{version.Major}.{version.Minor}.{version.Build}" : "unknown";
        ExtensionVersion = versionText;
        FeedbackBody = $"**Extension Info**: Version: {versionText}\n\n";

        // Feedback: handle type switching and submit
        SubmitFeedbackCommand = new((parameter, ct) =>
        {
            if (parameter is "SetTypeBug" or "SetTypeFeature")
            {
                var oldPrefix = FeedbackType == "Bug" ? "BUG: " : "FEATURE: ";
                var newType = parameter is "SetTypeBug" ? "Bug" : "Feature";
                var newPrefix = newType == "Bug" ? "BUG: " : "FEATURE: ";
                FeedbackType = newType;

                // Flip the prefix in the title
                if (FeedbackTitle.StartsWith(oldPrefix, StringComparison.Ordinal))
                    FeedbackTitle = newPrefix + FeedbackTitle[oldPrefix.Length..];
                else if (!FeedbackTitle.StartsWith(newPrefix, StringComparison.Ordinal))
                    FeedbackTitle = newPrefix + FeedbackTitle;

                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(FeedbackTitle) || FeedbackTitle.Trim() is "BUG:" or "FEATURE:")
            {
                FeedbackStatus = "Please enter a title.";
                return Task.CompletedTask;
            }

            var label = FeedbackType == "Bug" ? "bug" : "enhancement";
            var title = Uri.EscapeDataString(FeedbackTitle.Trim());
            var body = Uri.EscapeDataString(FeedbackBody.Trim());
            var url = $"https://github.com/ardimedia-com/visualstudio-task-runner-extended/issues/new?title={title}&body={body}&labels={label}";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

            FeedbackStatus = "Opened in browser.";
            var prefix = FeedbackType == "Bug" ? "BUG: " : "FEATURE: ";
            FeedbackTitle = prefix;
            FeedbackBody = $"**Extension Info**: Version: {versionText}\n\n";
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
    public bool ShowTasks
    {
        get => _showTasks;
        set => SetProperty(ref _showTasks, value);
    }

    [DataMember]
    public bool ShowBackground
    {
        get => _showBackground;
        set => SetProperty(ref _showBackground, value);
    }

    [DataMember]
    public bool ShowFeedback
    {
        get => _showFeedback;
        set => SetProperty(ref _showFeedback, value);
    }

    [DataMember]
    public string FeedbackTitle
    {
        get => _feedbackTitle;
        set => SetProperty(ref _feedbackTitle, value);
    }

    [DataMember]
    public string FeedbackBody
    {
        get => _feedbackBody;
        set => SetProperty(ref _feedbackBody, value);
    }

    [DataMember]
    public string FeedbackType
    {
        get => _feedbackType;
        set => SetProperty(ref _feedbackType, value);
    }

    [DataMember]
    public string FeedbackStatus
    {
        get => _feedbackStatus;
        set => SetProperty(ref _feedbackStatus, value);
    }

    [DataMember]
    public string ExtensionVersion
    {
        get => _extensionVersion;
        set => SetProperty(ref _extensionVersion, value);
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
    public AsyncCommand DeleteGroupCommand { get; }

    [DataMember]
    public AsyncCommand RenameGroupCommand { get; }

    [DataMember]
    public AsyncCommand RemoveFromGroupCommand { get; }

    [DataMember]
    public AsyncCommand AddGroupCommand { get; }

    [DataMember]
    public AsyncCommand StartAllInSourceCommand { get; }

    [DataMember]
    public AsyncCommand StopAllInSourceCommand { get; }

    [DataMember]
    public AsyncCommand SubmitFeedbackCommand { get; }

    private TaskTreeNode? _selectedNode;
    private bool _hasDetails;
    private string _detailName = string.Empty;
    private string _detailStatus = string.Empty;
    private string _detailSource = string.Empty;
    private string _detailCommand = string.Empty;
    private string _detailWorkingDir = string.Empty;
    private string _detailType = string.Empty;

    [DataMember]
    public bool HasDetails
    {
        get => _hasDetails;
        set => SetProperty(ref _hasDetails, value);
    }

    [DataMember]
    public string DetailName
    {
        get => _detailName;
        set => SetProperty(ref _detailName, value);
    }

    [DataMember]
    public string DetailStatus
    {
        get => _detailStatus;
        set => SetProperty(ref _detailStatus, value);
    }

    [DataMember]
    public string DetailSource
    {
        get => _detailSource;
        set => SetProperty(ref _detailSource, value);
    }

    [DataMember]
    public string DetailCommand
    {
        get => _detailCommand;
        set => SetProperty(ref _detailCommand, value);
    }

    [DataMember]
    public string DetailWorkingDir
    {
        get => _detailWorkingDir;
        set => SetProperty(ref _detailWorkingDir, value);
    }

    [DataMember]
    public string DetailType
    {
        get => _detailType;
        set => SetProperty(ref _detailType, value);
    }

    /// <inheritdoc />
    protected override async Task OnSolutionOpenedAsync(CancellationToken cancellationToken)
    {
        // Check if a solution is actually loaded (base class may call this before solution is ready)
        var fingerprint = await GetSolutionFingerprintAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(fingerprint))
        {
            BuildEmptyTree();
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
                BuildEmptyTree();
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
        _taskNodeMap.Clear();
        _groupEntryNodes.Clear();
        _workspaceFolder = string.Empty;
        HasDetails = false;
        BuildEmptyTree();
        StatusText = "No solution loaded";
    }

    private void OnRefreshRequested()
    {
        StatusText = "Refreshing...";
        _ = Task.Run(async () =>
        {
            try { await OnSolutionOpenedAsync(CancellationToken.None).ConfigureAwait(false); }
            catch { /* ignore */ }
        });
    }

    private void OnStopAllRequested()
    {
        StatusText = "Stopping all tasks...";
        _ = Task.Run(async () =>
        {
            await _taskRunner.StopAllAsync().ConfigureAwait(false);
            foreach (var node in _taskNodeMap.Values)
            {
                node.Status = Models.TaskStatus.Idle;
            }
            RefreshGroupsInTree();
            StatusText = "All tasks stopped.";
        });
    }

    private void OnCollapseAllRequested()
    {
        foreach (var node in TreeItems)
        {
            CollapseRecursive(node);
        }
    }

    private static void CollapseRecursive(TaskTreeNode node)
    {
        node.IsExpanded = false;
        foreach (var child in node.Children)
        {
            CollapseRecursive(child);
        }
    }

    private void OnTabChanged(string tab)
    {
        _activeTab = tab;
        ShowTasks = tab == "Tasks";
        ShowBackground = tab == "Background";
        ShowFeedback = tab == "Feedback";
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        // Unsubscribe from static event bus to prevent leaks
        ToolbarActionBus.RefreshRequested -= OnRefreshRequested;
        ToolbarActionBus.StopAllRequested -= OnStopAllRequested;
        ToolbarActionBus.CollapseAllRequested -= OnCollapseAllRequested;
        ToolbarActionBus.TabChanged -= OnTabChanged;

        // Unsubscribe from instance events
        _taskRunner.TaskStatusChanged -= OnTaskStatusChanged;

        // Stop and dispose resources
        _fileWatcher.Dispose();
        _taskRunner.Dispose();

        base.Dispose();
    }

    private void BuildEmptyTree()
    {
        TreeItems.Clear();

        var configFilesRoot = new TaskTreeNode("Available Configuration Files (Tasks)", TreeIcons.ConfigFiles)
        { SelectCommand = SelectNodeCommand, FontWeight = "Bold" };
        TreeItems.Add(configFilesRoot);

        var groupsRoot = new TaskTreeNode("Run Groups", TreeIcons.RunGroups)
        { SelectCommand = SelectNodeCommand, FontWeight = "Bold" };

        groupsRoot.Children.Add(new TaskTreeNode("Shared (commited)", TreeIcons.Folder)
        {
            GroupParam = "shared",
            AddGroupCommand = AddGroupCommand,
            AddGroupVisibility = "Visible",
            SelectCommand = SelectNodeCommand,
        });
        groupsRoot.Children.Add(new TaskTreeNode("Local (not commited)", TreeIcons.Folder)
        {
            GroupParam = "local",
            AddGroupCommand = AddGroupCommand,
            AddGroupVisibility = "Visible",
            SelectCommand = SelectNodeCommand,
        });

        TreeItems.Add(groupsRoot);
    }

    private void UpdateDetailsPane(TaskTreeNode? node)
    {
        if (node?.Task is null)
        {
            HasDetails = false;
            return;
        }

        var task = node.Task;
        DetailName = task.Label;
        DetailStatus = node.Status switch
        {
            Models.TaskStatus.Running => "Running",
            Models.TaskStatus.Error => "Error",
            _ => "Idle",
        };
        DetailSource = task.Source.FilePath;
        DetailCommand = task.IsCompound
            ? $"compound ({task.DependsOrder}): {string.Join(", ", task.DependsOn)}"
            : task.Args.Length > 0
                ? $"{task.Command} {string.Join(' ', task.Args)}"
                : task.Command;
        DetailWorkingDir = task.WorkingDirectory ?? "(source file directory)";
        DetailType = task.TaskType == TaskType.Background ? "background" : "normal";
        HasDetails = true;
    }

    private void OnTaskStatusChanged(TaskItem task, Models.TaskStatus status)
    {
        var key = $"{task.Source.FilePath}::{task.Label}";
        if (_taskNodeMap.TryGetValue(key, out var node))
        {
            node.Status = status;
        }

        // Update group entry nodes for this task
        UpdateGroupEntryStatus(task.Label, status);

        // Refresh groups to update group-level CanStart/CanStop
        RefreshGroupsInTree();

        // Update details pane if the changed task is currently selected
        if (_selectedNode?.Task is not null && _selectedNode.Task.Label == task.Label)
        {
            UpdateDetailsPane(_selectedNode);
        }
    }


    private void BuildGroupsTree()
    {
        // Remove existing groups root if present
        var existingGroupsRoot = TreeItems.FirstOrDefault(n => n.Name == "Run Groups");
        if (existingGroupsRoot is not null)
        {
            TreeItems.Remove(existingGroupsRoot);
        }

        _groupEntryNodes.Clear();

        var groupsRoot = new TaskTreeNode("Run Groups", TreeIcons.RunGroups)
        { SelectCommand = SelectNodeCommand, FontWeight = "Bold" };

        // Shared file node
        var sharedNode = new TaskTreeNode("Shared (commited)", TreeIcons.Folder)
        {
            GroupParam = "shared",
            AddGroupCommand = AddGroupCommand,
            AddGroupVisibility = "Visible",
            SelectCommand = SelectNodeCommand,
        };

        // Local file node
        var localNode = new TaskTreeNode("Local (not commited)", TreeIcons.Folder)
        {
            GroupParam = "local",
            AddGroupCommand = AddGroupCommand,
            AddGroupVisibility = "Visible",
            SelectCommand = SelectNodeCommand,
        };

        if (!string.IsNullOrEmpty(_workspaceFolder))
        {
            var sharedGroups = _groupConfigService.LoadSharedGroups(_workspaceFolder);
            var localGroups = _groupConfigService.LoadLocalGroups(_workspaceFolder);

            BuildGroupNodes(sharedGroups, sharedNode, "shared");
            BuildGroupNodes(localGroups, localNode, "local");
        }

        groupsRoot.Children.Add(sharedNode);
        groupsRoot.Children.Add(localNode);
        TreeItems.Add(groupsRoot);
    }

    private void BuildGroupNodes(List<Models.TaskGroup> groups, TaskTreeNode parentNode, string prefix)
    {
        foreach (var group in groups)
        {
            var prefixedName = $"{prefix}:{group.Name}";
            var groupNode = new TaskTreeNode(group.Name, TreeIcons.Group)
            {
                GroupParam = prefixedName,
                StartStopVisibility = "Visible",
                GroupManageVisibility = "Visible",
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
                var isRunning = taskNode is not null && taskNode.Status == Models.TaskStatus.Running;
                if (isRunning) hasRunningTasks = true;

                var icon = taskNode is null ? TreeIcons.ParseError
                    : isRunning ? TreeIcons.TaskRunning
                    : taskNode.Status == Models.TaskStatus.Error ? TreeIcons.TaskError
                    : TreeIcons.TaskIdle;

                var entryNode = new TaskTreeNode(entry.Task, icon)
                {
                    Metadata = taskNode is null ? " (not found)" : $" ({entry.StartOrder})",
                    GroupParam = entry.Task,
                    StartStopVisibility = "Visible",
                    RemoveFromGroupVisibility = "Visible",
                    CanStart = taskNode is not null && !isRunning,
                    CanStop = isRunning,
                    StartCommand = StartTaskCommand,
                    StopCommand = StopTaskCommand,
                    SelectCommand = SelectNodeCommand,
                    RemoveFromGroupCommand = RemoveFromGroupCommand,
                    RemoveFromGroupParam = $"{prefixedName}|{entry.Task}",
                };
                groupNode.Children.Add(entryNode);

                // Track entry nodes so we can update their icons when status changes
                if (!_groupEntryNodes.TryGetValue(entry.Task, out var entries))
                {
                    entries = [];
                    _groupEntryNodes[entry.Task] = entries;
                }
                entries.Add(entryNode);
            }

            groupNode.CanStart = !hasRunningTasks;
            groupNode.CanStop = hasRunningTasks;

            parentNode.Children.Add(groupNode);
        }
    }

    private void UpdateGroupEntryStatus(string taskLabel, Models.TaskStatus status)
    {
        if (!_groupEntryNodes.TryGetValue(taskLabel, out var entryNodes)) return;

        var newIcon = status switch
        {
            Models.TaskStatus.Running => TreeIcons.TaskRunning,
            Models.TaskStatus.Error => TreeIcons.TaskError,
            _ => TreeIcons.TaskIdle,
        };

        foreach (var entryNode in entryNodes)
        {
            entryNode.Icon = newIcon;
            entryNode.CanStart = status != Models.TaskStatus.Running;
            entryNode.CanStop = status == Models.TaskStatus.Running;
        }

        // Update group-level CanStart/CanStop
        var groupsRoot = TreeItems.FirstOrDefault(n => n.Name == "Run Groups");
        if (groupsRoot is null) return;

        // Groups are now nested under file nodes (Shared/Local)
        foreach (var fileNode in groupsRoot.Children)
        {
            foreach (var groupNode in fileNode.Children)
            {
                var hasRunning = groupNode.Children.Any(c => c.Icon == TreeIcons.TaskRunning);
                groupNode.CanStart = !hasRunning;
                groupNode.CanStop = hasRunning;
            }
        }
    }

    private void RefreshGroupsInTree()
    {
        BuildGroupsTree();
    }

    private TaskTreeNode? FindTaskNode(string nameOrKey)
    {
        // Try exact key match first (Source.FilePath::Label)
        if (_taskNodeMap.TryGetValue(nameOrKey, out var node))
            return node;

        // Fall back to name match (for groups and compound tasks that reference by label)
        return _taskNodeMap.Values.FirstOrDefault(n => n.Name == nameOrKey);
    }

    /// <summary>
    /// Parses a prefixed group parameter "shared:GroupName" or "local:GroupName".
    /// Returns (groupName, isShared).
    /// </summary>
    private static (string GroupName, bool IsShared) ParseGroupParam(string param)
    {
        if (param.StartsWith("shared:", StringComparison.Ordinal))
            return (param[7..], true);
        if (param.StartsWith("local:", StringComparison.Ordinal))
            return (param[6..], false);
        return (param, false);
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

            var sourceGroups = tasks.GroupBy(t => t.Source.FilePath);
            foreach (var group in sourceGroups)
            {
                var firstTask = group.First();
                var sourceIcon = TreeIcons.ForSourceKind(firstTask.Source.Kind);
                var displayName = GetRelativeSourceName(firstTask.Source.FilePath);
                var sourceNode = new TaskTreeNode(displayName, sourceIcon)
                {
                    GroupParam = displayName,
                    StartStopVisibility = "Visible",
                    CanStart = true,
                    StartCommand = StartAllInSourceCommand,
                    StopCommand = StopAllInSourceCommand,
                    SelectCommand = SelectNodeCommand,
                };

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
                    var key = $"{task.Source.FilePath}::{task.Label}";
                    var taskNode = new TaskTreeNode(task.Label, task)
                    {
                        Metadata = metadata,
                        GroupParam = key,
                        StartStopVisibility = "Visible",
                        StartCommand = StartTaskCommand,
                        StopCommand = task.IsCompound ? null : StopTaskCommand,
                        AddToGroupCommand = AddToGroupCommand,
                        AddToGroupVisibility = "Visible",
                        SelectCommand = SelectNodeCommand,
                        BadgeIcon = task.IsAutoDiscovered ? TreeIcons.BadgeDotNet : string.Empty,
                        BadgeVisibility = task.IsAutoDiscovered ? "Visible" : "Collapsed",
                        BadgeTooltip = task.IsAutoDiscovered ? "Auto-discovered .NET CLI task" : string.Empty,
                    };
                    sourceNode.Children.Add(taskNode);

                    // Register in lookup map
                    _taskNodeMap[key] = taskNode;
                }

                parentNode.Children.Add(sourceNode);
            }
        }
    }

    private string GetRelativeSourceName(string filePath)
    {
        if (string.IsNullOrEmpty(_workspaceFolder))
            return Path.GetFileName(filePath);

        // Make path relative to workspace folder
        var workspace = _workspaceFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (filePath.StartsWith(workspace, StringComparison.OrdinalIgnoreCase))
        {
            return filePath[workspace.Length..].Replace(Path.DirectorySeparatorChar, '/');
        }

        return filePath;
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
