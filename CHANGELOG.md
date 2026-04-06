# Changelog

All notable changes to this project will be documented in this file.

## [0.3.6] - 2026-04-06

### Added

- **Collapse All**: Toolbar button now collapses all tree view nodes (bound to `IsExpanded` property per node)

## [0.3.4] - 2026-04-06

### Added

- **Auto-discover dotnet CLI tasks**: `dotnet: watch` for web projects, `dotnet: test` and `dotnet: test (watch)` for test projects — auto-generated from .csproj SDK and PackageReferences
- **Badge icon**: Auto-discovered .NET tasks show a small NuGet badge with tooltip "Auto-discovered .NET CLI task"
- **Unique Output Window panes**: Pane names now include the source file (e.g. "Task: dotnet: test [MyApp.Tests.csproj]") to prevent collisions

### Fixed

- **Resource leak on window close**: ViewModel now properly disposes TaskRunner, FileWatcher, and unsubscribes from static ToolbarActionBus events in Dispose()
- **dotnet watch test failure**: Removed redundant `--project` argument that caused "Metadata file not found" errors with project references
- **Serialization race condition**: Removed `BuildEmptyTree()` from constructor to prevent collection modification during Remote UI initialization
- **Project configuration**: Removed legacy VSSDK properties (`IsVisualStudioExtension`, `VssdkCompatibleExtension`, `DeployExtension`) to match official VS Extensibility SDK samples

## [0.3.0] - 2026-04-04

### Added

- **Shared/Local Group Files**: Run Groups tree now shows two sub-nodes: "Shared (commited)" and "Local (not commited)", each managing their own config file separately
- **Add to Group file selection**: "Add to Group..." now prompts whether to save to the shared or local file before entering the group name
- **Toolbar Tabs**: Three toggle buttons (Tasks, Background, Feedback) in the toolbar, switching between content panels
- **Details Pane**: Bottom panel showing selected task details (command, working directory, type, status) with selectable/copyable text
- **Background Tab**: Extension info page showing supported task sources, features, run group file documentation, and `.gitignore` guidance
- **Feedback Tab**: GitHub issue form with Bug/Feature type toggle (auto-prefixes title with BUG:/FEATURE:), description pre-filled with version info, and Extension Info section
- **Relative Source Paths**: Config file nodes show paths relative to workspace (e.g. `Bvd.Li.Web.Ui.Blazor/package.json` instead of just `package.json`)
- **Left-click Selection**: Tree nodes are now selectable via left-click (previously right-click only)
- **Empty Tree Structure**: Shows "Available Configuration Files" and "Run Groups" root nodes even before a solution is loaded
- **Add Group on file nodes**: "Add Group..." context menu item on "Shared (commited)" and "Local (not commited)" nodes to create empty groups directly
- **Start/Stop All on source files**: Right-click a source file node (e.g. `package.json`) to start or stop all tasks from that file
- **Context menu visibility**: Menu items are now shown/hidden per node type (no more disabled items on irrelevant nodes)
- **Debug Deployment**: Added `DeployExtension` property for experimental instance debugging

### Fixed

- **Run Group Icons**: Task status icons (running/idle/error) now update correctly in the Run Groups tree, not just in Available Configuration Files
- **Individual Task Start/Stop in Groups**: Group entry nodes now have Start/Stop context menu items with proper enabled/disabled state
- **Group CanStart/CanStop**: Group-level start button is now disabled when tasks are already running
- **Duplicate Source Nodes**: Config files with the same name in different directories (e.g. two `package.json`) now show as separate nodes with relative paths
- **BooleanToVisibilityConverter**: Fixed XAML crash in Remote UI by using standard WPF converter

### Changed

- **Folder Icons**: All config file source nodes and group nodes now use consistent folder icons
- **Group Delete/Rename**: Now targets only the specific file (shared or local) instead of both

### Removed

- **"+ New Group..." node**: Removed unused tree node (groups are created via "Add to Group..." context menu)
- **CreateGroupCommand**: Removed unused command and associated prompt method

## [0.1.1]

### Added (Phase 5: Polish and Marketplace)

- **Empty State**: Helpful hints when no task sources are found (suggests tasks.json, package.json, .csproj)
- **Toolbar Commands Wired**: Refresh (rescans), Stop All (stops all running tasks), Collapse All via ToolbarActionBus
- **Bold Root Items**: "Available Configuration Files" and "Run Groups" displayed in bold
- **Updated README**: Full feature list, usage guide, run groups documentation, technical details
- **Extension Icons**: Custom icon (play button + task dots on blue gradient) in 6 sizes (16-256px)
- **Marketplace Overview**: .claude/overview.md for VS Marketplace description
- **Deploy Guide**: .claude/deploy.md with publishing checklist
- **Publish Manifest**: Updated with tags (task-runner, npm, docker-compose, etc.) and categories (coding, build)

### Added (Phase 4: Advanced Output)

- **ProblemMatcher**: Detects errors and warnings in task output (MSBuild, TypeScript, npm, "Build FAILED" patterns)
- **Problem Summary**: Shows "--- Problems: X error(s), Y warning(s) ---" at end of task output
- **Error Detection**: Task status changes to Error when problems detected (even with exit code 0)

### Added (Phase 3: Grouping)

- **TaskGroup model**: TaskGroup, TaskGroupEntry, TaskGroupConfig
- **GroupConfigService**: Read/write/merge task-runner-extended-am.json (shared) and .local.json (per-user), atomic writes, concurrent access handling
- **Run Groups tree**: Real groups from config files displayed in the tree with group icon
- **Add to Group**: Context menu command to add any task to a group
- **New Group**: Create new groups via "+ New Group..." node in the tree
- **Start/Stop Group**: Start all tasks in a group (parallel or sequential), stop all tasks
- **Delete Group**: Remove groups via context menu
- **Merge Logic**: Local groups override shared groups with the same name
- **Rename Group**: VS input prompt for new name
- **Remove from Group**: Remove individual tasks from a group via context menu
- **User Prompts**: VS InputPromptOptions for group name input, PromptOptions.OKCancel for delete confirmation
- **Dynamic Group Status**: Task icons in groups reflect running state, group CanStart/CanStop updates automatically on task status changes

### Added (Phase 2: Multi-Source)

- **PackageJsonDiscoverer**: npm/pnpm/yarn script discovery with lock file detection (pnpm > yarn > npm priority), background task heuristics, pre/post script filtering
- **TasksVsJsonDiscoverer**: Visual Studio native tasks.vs.json format (taskLabel, appliesTo, msbuild type)
- **ComposeYmlDiscoverer**: Docker Compose services from compose.yml/docker-compose.yml with "all services" task, image metadata (requires YamlDotNet)
- **LaunchSettingsDiscoverer**: .NET launch profiles (Project, Executable, Docker) with env vars and application URL metadata
- **Compound Tasks**: dependsOn + dependsOrder (parallel/sequence) support for tasks.json, compound tasks start all dependencies
- **Extended Variables**: ${env:VARIABLE}, ${workspaceFolderBasename}, ${cwd} resolution
- **Parent Directory Scan**: Discovers tasks from up to 3 parent directories above workspace, shown as [Parent: path] nodes
- **CompoundTask Icon**: GroupByType KnownMoniker for compound tasks

### Added (Phase 1: Foundation)

- **Task Discovery**: tasks.json parser and .csproj MSBuild target discovery (SDK-style + .NET Framework)
- **Task Execution**: Start/stop tasks via Process.Start with Output Window Pane streaming
- **Process Tree Management**: Windows Job Objects for reliable process tree kill and graceful shutdown
- **Unified Tree View**: Hierarchical tree with VS KnownMoniker icons, custom ControlTemplate with full-row hover/selection highlighting
- **Context Menu**: Right-click Start/Stop/Add to Group with enabled/disabled based on task status
- **Right-click Selection**: Custom selection via vs:EventHandler on MouseRightButtonDown
- **Toolbar**: Refresh, Stop All, Collapse All buttons in tool window header
- **Sidebar Placement**: Docked alongside Solution Explorer via DockedTo(SolutionExplorerGuid)
- **Status Icons**: Idle (Run), Running (Sync), Error (StatusError) with automatic switching
- **File Watching**: FileSystemWatcher with 500ms debounce on all task source files, auto-rescan on changes
- **Variable Resolution**: ${workspaceFolder} support for tasks.json commands and arguments
- **Error Handling**: Parse errors shown as warning nodes in the tree (malformed JSON/XML)
- **ANSI Strip**: Removes ANSI escape codes from task output, NO_COLOR env var set
- **Solution Detection**: Waits for solution to load, configurable polling/debounce timing via Ardimedia.VsExtensions.Common 1.0.8
- **Models**: TaskItem, TaskSource, TaskSourceKind, TaskStatus, TaskType
- **16 Unit Tests**: TasksJsonDiscoverer, CsprojDiscoverer, VariableResolver (all passing)
- **FUNDING.yml**: GitHub Sponsors configuration

### Phase 1 Spike Results

- TreeView + HierarchicalDataTemplate: works in Remote UI
- ContextMenu: works in Remote UI
- Sidebar Placement (DockedTo SolutionExplorer): works
- VS Internal Terminal API: not possible (filed microsoft/VSExtensibility#560)
- Drag-and-Drop in Remote UI: not possible (filed microsoft/VSExtensibility#561)

### Infrastructure

- Initial project structure (extension skeleton, CI/CD, test project)
- Feature specification (.claude/feature.md)
- CLI reference documentation (.claude/reference-cli.md)
- Task configuration file reference (.claude/reference-task-config-files.md)
- GitHub Actions: build.yml (push/PR) and release.yml (tag-based)
- README.md with feature overview and task sources table
