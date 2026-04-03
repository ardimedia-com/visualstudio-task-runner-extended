---
status: Draft
updated: 2026-04-03 23:00h
references:
  - .claude/reference-cli.md — CLI reference (dotnet, npm, docker, VS launch functions)
  - .claude/reference-task-config-files.md — Task configuration file formats
---

# Visual Studio Task Runner Extended

## Vision

A Visual Studio extension that simplifies developer workflows for multi-tool projects. The extension discovers task definitions from 8 sources (tasks.json, tasks.vs.json, package.json, .csproj, launchSettings.json, Gruntfile.js, gulpfile.js, compose.yml), displays them in a unified view, and allows grouping and parallel execution — directly in Visual Studio.

Bridges the gap between VS Code (tasks.json with compound tasks) and Visual Studio (no equivalent).

## Problem

Visual Studio offers no way to start multiple background processes (e.g., `npm run watchcss` + `dotnet watch` + `docker compose up`) in a coordinated manner. Developers must:

- Manually open terminals and type commands
- Remember which processes need to be running
- Switch between VS Code (tasks.json) and Visual Studio (no support)
- Manage npm scripts, MSBuild targets, Docker containers, and launch profiles in different locations

For a detailed comparison of what Visual Studio offers natively vs what this extension adds, see [VS Launch Functions in reference-cli.md](reference-cli.md).

## User Stories

### Core Workflows

1. **As a Blazor developer**, I want to start `npm run watchcss` and `dotnet watch` with a single click, so I don't have to manually open two terminals.

2. **As a developer in a multi-project repository**, I want to see all available tasks from different sources (tasks.json, package.json, .csproj, compose.yml) in a unified view.

3. **As a team lead**, I want to define task groups and share them via the repository, so new team members can start the correct development environment with one click.

4. **As a developer switching between VS Code and Visual Studio**, I want to use my existing tasks.json definitions in Visual Studio without redefining them.

5. **As a developer**, I want to see which background processes are currently running, and stop them individually or as a group.

6. **As a developer**, I want to be notified when a task fails, so I notice problems immediately.

## Architecture Overview

```
 Task Source Files                Extension Core                    UI / Output
+-------------------+     +-------------------------+     +-------------------+
| .vscode/tasks.json|     |                         |     |                   |
| tasks.vs.json     |---->|  Discovery               |     |  Tool Window      |
| package.json      |     |    |                     |     |    > Config Files |
| *.csproj          |     |  Parsing                 |---->|    > Run Groups   |
| launchSettings    |     |    |                     |     |    (unified tree) |
| Gruntfile.js      |     |  Task Model              |     |                   |
| gulpfile.js       |     |    |                     |     |  Context Menus    |
| compose.yml       |     |  Process Management      |     |                   |
+-------------------+     |    |                     |     |  Output Panes     |
                          |  File Watching           |     |                   |
+-------------------+     +-------------------------+     +-------------------+
| task-runner-      |          |
| extended-am.json  |<---------+ (groups config)
| *.local.json      |
+-------------------+
```

**Flow:**
1. **Discovery** — Scan project, solution, and parent directories for task source files
2. **Parsing** — Read each file format and extract tasks into a unified Task Model
3. **UI** — Display tasks in a unified tree view, allow grouping via drag-and-drop
4. **Execution** — Start/stop tasks via Process Management (Job Objects, output streaming)
5. **File Watching** — Monitor source files for changes, refresh the task list automatically

## Task Sources

The extension discovers tasks from **8 sources**. For detailed file format documentation, see [reference-task-config-files.md](reference-task-config-files.md). For CLI command details, see [reference-cli.md](reference-cli.md).

### 1. .vscode/tasks.json

The primary, IDE-agnostic task format. The extension reads labels, commands, arguments, working directories, compound task dependencies (`dependsOn`), and background task flags. For the full property reference, see [reference-task-config-files.md](reference-task-config-files.md).

Supported subset:
- Phase 1: `label`, `command`, `args`, `cwd`, `isBackground`, `type`, `group`
- Phase 2: `options.env`, `dependsOn`, `dependsOrder`, `presentation`, `windows` overrides
- Phase 4: `problemMatcher`
- Not planned: `input` variables, `runOptions`

Unknown properties are silently ignored. Tasks containing unresolved `${input:...}` variables are marked as "not startable" with a clear error message.

### 2. tasks.vs.json

Visual Studio's native task format for unrecognized codebases (Open Folder mode). The extension reads task labels, commands, and `appliesTo` patterns. Note: This format is rarely used in .NET projects — it primarily serves CMake, Makefile, and other non-.NET codebases.

### 3. package.json (npm/pnpm/yarn Scripts)

Reads the `scripts` section and detects the package manager via lock files (`package-lock.json` → npm, `pnpm-lock.yaml` → pnpm, `yarn.lock` → yarn). If multiple lock files exist, priority: pnpm > yarn > npm. Each script is displayed as a task (e.g., `npm: buildcss`). Background task detection uses heuristics (script name contains `watch`/`dev`/`serve`, or command contains `--watch`).

### 4. .csproj MSBuild Targets

Discovers custom `<Target>` elements with `<Exec Command="...">` in .csproj files. Supports **both** SDK-style (.NET Core/5+) and non-SDK-style (.NET Framework) projects from Phase 1:

- **SDK-style**: Detects via `Sdk` attribute on `<Project>`. Runs targets via `dotnet msbuild -t:TargetName`.
- **Non-SDK-style**: Detects via `ToolsVersion` attribute or absence of `Sdk`. Runs targets via `msbuild.exe -t:TargetName`. Path resolved via `vswhere.exe`.
- Recognizes both naming conventions: `BeforeTargets="Build"` (SDK) and `BeforeBuild` target name (.NET Framework)
- Only top-level targets in the .csproj itself (no imported .targets files)

### 5. launchSettings.json

Reads launch profiles from `Properties/launchSettings.json`. Displays all profiles (Project, Executable, Docker), allows switching the default profile, and shows environment variables. Only exists in .NET (Core/5+) projects — .NET Framework projects use different mechanisms (see [reference-task-config-files.md](reference-task-config-files.md)).

### 6. Gruntfile.js

Discovers Grunt tasks via `grunt --help --no-color` (shell out). Displays registered tasks (e.g., `grunt: build`, `grunt: watch`). Requires `grunt-cli` and `node_modules` to be installed — shows a hint if missing. Grunt is considered legacy but still found in existing enterprise projects.

### 7. gulpfile.js

Discovers Gulp tasks via `gulp --tasks-json` (shell out). Displays named tasks (e.g., `gulp: build`, `gulp: styles`). Requires `gulp-cli` and `node_modules` to be installed — shows a hint if missing. Recognizes all gulpfile variants: `gulpfile.js`, `gulpfile.ts`, `gulpfile.babel.js`. Like Grunt, Gulp is legacy but still actively used in projects with complex asset pipelines.

### 8. compose.yml (Docker Compose)

Discovers services defined in `compose.yml` / `docker-compose.yml`. Each service is displayed as a startable task (e.g., `docker: db`, `docker: redis`). Supports starting individual services or all services together. Recognizes `compose.override.yml` for local overrides.

## Hierarchy Discovery

The extension searches for tasks not only in the current project folder but also in parent directories:

```
D:\CODE\amvs\                          <- Root: .vscode/tasks.json, package.json
+-- bvd.li.web\                        <- Solution: .vscode/tasks.json, package.json, compose.yml
|   +-- Bvd.Li.Web.Ui.Blazor\         <- Project: package.json, .csproj, launchSettings.json
|   +-- Bvd.Li.Web.Domain\            <- Project: .csproj
```

Display in the extension (Available Configuration Files tree):

```
> Solution: bvd.li.web
  > .vscode/tasks.json
    o watchcss
    o dotnet-watch
    * dev (compound: watchcss + dotnet-watch)
  > compose.yml
    o docker: db
    o docker: redis
  > Bvd.Li.Web.Ui.Blazor
    > package.json
      o npm: buildcss
      o npm: watchcss
    > Bvd.Li.Web.Ui.Blazor.csproj
      o msbuild: WatchTailwindCss (Debug)
      o msbuild: BuildTailwindCss (Release)
    > launchSettings.json
      o https (Project, Debug)
  > Bvd.Li.Web.Domain
    > Bvd.Li.Web.Domain.csproj
      (no custom targets)
> [Parent: D:\CODE\amvs]
  > package.json
    o npm: build
    o npm: lint
```

Parent directories above the workspace (solution/.git root) are shown with a `[Parent: path]` prefix to clearly distinguish them from project-level files. The workspace boundary is the directory containing the `.sln`/`.slnx` file or the `.git` folder.

### File Discovery Order

1. **Current project directory** — .csproj, package.json, Properties/launchSettings.json, Gruntfile.js, gulpfile.js
2. **Solution directory** — .vscode/tasks.json, .vs/tasks.vs.json, package.json, compose.yml
3. **Parent directories** (up to `maxParentDepth`) — .vscode/tasks.json, package.json

### Exclusions

These directories are **never** scanned:
- `node_modules`
- `.git`
- `bin`, `obj`
- `.vs` (except for tasks.vs.json)
- `packages` (NuGet packages folder)

Maximum upward search depth is configurable (default: 3).

### Solution Filters (.slnf)

When a Solution Filter is open, the extension respects the filter:
- **Solution-level files** (tasks.json, compose.yml) are always shown
- **Project-level files** (.csproj, package.json, launchSettings.json) are only shown for projects loaded in the filter
- Projects excluded by the filter are not scanned

## Task Groups

### Data Model

Task groups (start profiles) bundle multiple tasks that are started together. The extension reads **both** config files and merges them:

- `task-runner-extended-am.json` — shared with team, committed to git
- `task-runner-extended-am.local.json` — per-user, gitignored (`.gitignore` rule: `*.local.json`)

The local file takes precedence: if a group with the same name exists in both files, the local version wins. New groups created by the user are saved to the local file by default (configurable via `taskRunnerExtended.newGroupsLocation`).

The `am` prefix (ardimedia) prevents naming collisions with other extensions or Visual Studio defaults. Both files use the same schema and are placed in the project/solution root.

```json
{
  "version": "1.0",
  "groups": [
    {
      "name": "Development",
      "icon": "play",
      "tasks": [
        {
          "source": "compose.yml",
          "task": "db",
          "startOrder": "sequence",
          "order": 1
        },
        {
          "source": ".vscode/tasks.json",
          "task": "watchcss",
          "startOrder": "parallel"
        },
        {
          "source": ".vscode/tasks.json",
          "task": "dotnet-watch",
          "startOrder": "parallel"
        }
      ]
    },
    {
      "name": "Build Production",
      "icon": "package",
      "tasks": [
        {
          "source": "package.json",
          "task": "buildcss",
          "startOrder": "sequence",
          "order": 1
        },
        {
          "source": ".csproj",
          "task": "BuildTailwindCss",
          "startOrder": "sequence",
          "order": 2
        }
      ]
    }
  ]
}
```

### Group Properties

| Property | Type | Description |
|---|---|---|
| `name` | string | Display name of the group |
| `icon` | string | Icon identifier (play, package, debug, etc.) |
| `tasks` | array | List of task references |

### Task Reference Properties

| Property | Type | Description |
|---|---|---|
| `source` | string | Relative path to the source file (e.g., `.vscode/tasks.json`, `compose.yml`) |
| `task` | string | Task label/name within that source |
| `startOrder` | `"parallel"` \| `"sequence"` | How this task starts relative to others |
| `order` | number | Execution order for sequential tasks (lower = first) |

### Version Migration

When the extension updates the schema:
- The `version` field determines the expected format
- Older versions are automatically migrated to the latest format
- A backup of the original file is created before migration

### UI in Tool Window (Run Groups Root Node)

- Run Groups are a root node in the unified tree view (see Tool Window section)
- Drag-and-drop from the Available Configuration Files root node to add tasks to groups
- Inline creation via "New Group..." node
- Edit: name, icon, order, parallel/sequential

### Interaction

Tasks and groups are started the same way — no separate UI needed:
- **Double-click** on a task or group → start it
- **Context menu** on a task → Start, Stop, Edit Source File, Add to Group
- **Context menu** on a group → Start All, Stop All, Edit, Delete
- Starting a group starts all tasks in that group (sequentially or in parallel, as configured)

## Tool Window

A single dockable tool window placed in the **sidebar** (same area as Solution Explorer, Git Changes, GitHub Copilot Chat) with a **unified tree view** — no tabs. Tasks and Groups are root nodes in the same tree. Running status is shown inline via visual indicators.

```
Task Runner Extended

> Available Configuration Files (Tasks)
  > .vscode/tasks.json
    o  watchcss
    >  dotnet-watch              (running)
    *  dev (compound)
  > compose.yml
    o  docker: db
    >  docker: redis             (running)
  > package.json
    o  npm: buildcss
    >  npm: watchcss             (running)
  > Bvd.Li.Web.Ui.Blazor.csproj
    o  msbuild: WatchTailwindCss
    o  msbuild: BuildTailwindCss

> Run Groups
  > Development                 >> (3 running)
    >  docker: db                (running)
    >  watchcss                  (running)
    >  dotnet-watch              (running)
  > Build Production
    o  npm: buildcss
    o  msbuild: BuildTailwindCss
  + New Group...                 (drag tasks here)
```

### Available Configuration Files (Tasks) Root Node

- Tree view of all discovered tasks from all 8 sources
- Grouped by source file (tasks.json, compose.yml, package.json, .csproj, etc.)
- Hierarchy display (Project -> Solution -> Parent)
- Parent directories (above the workspace) shown with their path as prefix (e.g., `[Parent: D:\CODE\amvs] package.json`)
- Status indicators: o idle / > running / x error

### Run Groups Root Node

- Displays all defined run groups
- Drag-and-drop from Available Configuration Files to add tasks to a group (or context menu "Add to Group..." if drag-drop not feasible in Remote UI)
- "New Group..." node at the bottom to create groups inline
- Group-level status: shows count of running tasks (e.g., ">> 3 running")
- Context menu on task within group: additional options (Remove from Group, Reorder)

### Running Status (inline, no separate tab)

Instead of a dedicated "Running" tab, running tasks are visually indicated throughout the tree:
- **Task level**: Icon changes from o (idle) to > (running) to x (error)
- **Group level**: Shows aggregate status (e.g., "3 running")
- **Click on running task** -> focus the task's terminal tab or Output Window Pane
- **Filter** (optional): "Show running only" to temporarily filter the tree

## Process Management

### Starting Tasks

Command construction depends on the `type` field in tasks.json:
- `"type": "shell"` (default) → wraps the command in `cmd.exe /c <command>` (enables PATH resolution, pipes, redirects)
- `"type": "process"` → executes the command directly (no shell wrapper, faster, but no shell features)

**If VS Internal Terminal is available (spike success):**
Both shell and process commands are sent to a named terminal tab. The terminal handles execution, output display, and encoding. The extension only needs to open the tab, send the command, and track the process.

**If Output Window Pane fallback:**
The extension uses `Process.Start` directly, reads stdout/stderr via `OutputDataReceived`/`ErrorDataReceived` events, and streams output to the pane. In this mode, encoding must be handled explicitly: `Process.StartInfo.StandardOutputEncoding = Encoding.UTF8` or prepend `chcp 65001`, because `cmd.exe` defaults to CP437/CP1252 on Windows.

Additional details:
- .NET Framework projects: `msbuild.exe` instead of `dotnet msbuild` (path resolved via `vswhere.exe`)
- Environment variables from `options.env` (tasks.json) and `environmentVariables` (launchSettings.json) are added to the process environment (additive, not replacing system variables)
- `npx`, `pnpm dlx`, `yarn dlx` commands are treated as normal shell commands (no special parsing)

### Task Type Classification

The extension classifies every task as either **normal** or **background**:

| Aspect | Normal Task | Background Task |
|---|---|---|
| Behavior | Starts, runs, exits on its own | Starts and runs indefinitely until stopped |
| Example | `dotnet build`, `npm run buildcss` | `dotnet watch`, `npm run watchcss` |
| Exit | Natural exit (exit code 0 or error) | Never exits on its own |
| Tree status | idle → running → idle/error | idle → running (until manually stopped) |
| In sequence | Next task waits for exit code 0 | Next task starts immediately (no waiting) |
| Terminal tab | Can auto-close after exit (`keepOutputOnStop`) | Stays open as long as the task runs |

The classification is determined automatically based on the command. For the full classification table, see [reference-cli.md](reference-cli.md).

Key rules:
- `dotnet watch`, `docker compose up` (without -d) → **background task**
- `dotnet run`, `dotnet build`, `dotnet test`, `docker compose up -d` → **normal task**
- npm/pnpm/yarn scripts → heuristic: background if name contains `watch`/`dev`/`serve`/`start`, or command contains `--watch`/`concurrently`
- Grunt/Gulp tasks → normal, except `watch` task → background
- **dotnet ef**: Should not be in auto-start groups, as migrations should not run automatically
- **docker compose up -d**: Useful as a sequential prerequisite (exits after starting containers)

### Stopping Tasks

- **Entire process tree** is terminated, not just the main process
- Implementation via **Windows Job Objects**: Each task process is placed in a Job Object. The Job Object automatically terminates all child processes.
- Graceful shutdown: First `Ctrl+C` signal (SIGINT), then force kill after 5 seconds
- This also solves the zombie process problem (when VS or the extension crashes)

### Default Working Directory

When no `cwd` is specified:
- tasks.json: Directory of the tasks.json file (= VS Code behavior)
- package.json: Directory of the package.json
- .csproj: Directory of the .csproj file
- launchSettings.json: Directory of the launchSettings.json
- Gruntfile.js / gulpfile.js: Directory of the file
- compose.yml: Directory of the compose.yml

### Compound Tasks (dependsOn)

- `dependsOrder: "parallel"` -> start all dependent tasks simultaneously
- `dependsOrder: "sequence"` -> start tasks one after another, next starts after exit code 0 of the previous
- Circular dependencies are detected and reported as errors
- For background tasks (`isBackground: true`) in sequence: Next task starts immediately (no waiting for exit)

### Solution/VS Close Behavior

When the solution or Visual Studio is closed:
- All running tasks are stopped (via Job Objects -> automatic)
- Configurable: Show confirmation dialog when tasks are running ("X tasks are still running. Close anyway?")

## Output Display

### Preferred: VS Internal Terminal

The preferred approach is to open a **terminal tab per task** in Visual Studio's built-in integrated terminal (View → Terminal). This gives developers a familiar, interactive terminal experience with real-time output.

Each task is **permanently linked** to its own terminal tab:

| Action | Terminal behavior |
|---|---|
| Start task | Open new terminal tab, named after the task (e.g., "watchcss") |
| Task running | Terminal shows live stdout/stderr output |
| Stop task | Process terminated, terminal tab stays open (output remains visible) |
| Restart task | **Reuse the same terminal tab** — clear output, start new process |
| Click task in tree | Focus the linked terminal tab |
| Close terminal tab | Optionally stop the running task (configurable) |

The extension maintains a `TaskItem → Terminal Tab ID` mapping to enable:
- Focusing the correct terminal tab when clicking a task in the tree
- Reusing tabs on restart instead of opening new ones
- Closing/stopping via either the tree or the terminal

However, the VisualStudio.Extensibility SDK does **not** expose an official Terminal API. The internal terminal may be accessible via:
- **VSSDK COM interfaces** (`IVsTerminalService` or similar) — requires investigation
- **Mixed-mode extension** (out-of-process + VSSDK-compatible in-process access)
- **DTE automation** — `DTE.ExecuteCommand("View.Terminal")` or similar

**Critical path**: This will be validated early in Phase 1 (after project setup, before building features). The spike must confirm that the API allows: opening a named tab, sending commands, reading output, closing a tab, and reusing an existing tab.

### Fallback: Output Window Pane

If the VS internal terminal cannot be controlled programmatically, the fallback is:

- One **Output Window Pane** per task (read-only)
- Pane name = task label (e.g., "Task: watchcss")
- Real-time output streaming
- Pane stays open after task ends (configurable)

### Fallback: External Terminal

For interactive tasks that require user input:
- `start /min cmd /c ...` (separate window outside VS)
- Only used when the other approaches can't handle the use case

### Output Options Summary

| Approach | Interactive | In VS | Effort | Status |
|---|---|---|---|---|
| **VS Internal Terminal** | Yes | Yes | Unknown | Preferred — validate in Phase 1 spike |
| **Output Window Pane** | No (read-only) | Yes | Low | Proven fallback |
| **External Terminal** | Yes | No | Low | Last resort |
| **Custom WPF Terminal** | Partial | Yes | High | Not planned (only if all above fail) |

### Output Buffer Management

Background tasks (watchcss, docker compose) run for hours and produce continuous output. To prevent memory leaks:
- Extension-internal output buffer is limited to **10'000 lines** per task (ring buffer)
- The Output Window Pane / terminal tab handles its own display buffer
- Process and Job Object handles are properly disposed when tasks stop

### Interactive Tasks (stdin)

Some tasks require interactive input (e.g., `docker login`, `npm publish` with OTP). The extension cannot predict which tasks are interactive. Tasks that hang waiting for input will show as "running" indefinitely. Users should start such tasks via "External Terminal" (context menu option) for full stdin support.

## Error Handling and Diagnostics

### Parse Errors

| Situation | Behavior |
|---|---|
| Syntactically invalid JSON file | Warning in tree: "! tasks.json: Parse Error (line 12)". Other sources still loaded. |
| Missing required fields (e.g., `command`) | Task displayed with warning but cannot be started |
| Unknown properties | Silently ignored (no warning) |
| Circular `dependsOn` references | Error in tree: "x Circular dependency: taskA -> taskB -> taskA" |
| `dependsOn` references non-existent task | Warning, compound task cannot be started |

### Runtime Errors

| Situation | Behavior |
|---|---|
| Command not in PATH | Error in Output Pane: "Command not found: tailwindcss". Task status -> error. |
| Permission denied | Error in Output Pane. Task status -> error. |
| Task exits with exit code != 0 | Task status -> error. Exit code shown in output. |
| Task process crashes | Task status -> error. Crash logged in diagnostics. |

### Diagnostic Logging

- Dedicated Output Window Pane: "Task Runner Extended - Diagnostics"
- Logging level configurable: Verbose / Info / Warning / Error
- Logs: Scanned files, discovered tasks, executed commands, exit codes, errors

## Security

### Workspace Trust

The extension executes arbitrary commands defined in project files. This is a security risk with cloned repositories.

Safeguards:
- **First-time execution**: When a task from a new source is started for the first time, display the full command and require confirmation
- **autoStartGroup**: Automatic start on Solution Open requires a one-time confirmation per solution ("This solution wants to start tasks automatically. Allow?")
- **Trusted folders**: List of folders that don't require confirmation (configurable)

## File Watching

The extension watches all discovered task source files for changes:

- `FileSystemWatcher` on all 8 source file types
- **Debounced refresh**: Update after 500ms without further changes (not on every keystroke)
- New/deleted task source files are automatically detected
- Running tasks are **not** automatically restarted on file changes
- `InternalBufferSize` set to 64KB+ to prevent buffer overflow during mass file changes (e.g., `npm install`)
- Duplicate events (FileSystemWatcher fires Changed+Changed) are deduplicated
- Also watches `task-runner-extended-am.json` and `task-runner-extended-am.local.json` for external changes (e.g., another VS instance editing the same file)

### Concurrent Access

Multiple VS instances may open the same solution simultaneously:
- **Reads** are safe (file-level locking by OS)
- **Writes** to config files use atomic operations (write to temp file, then `File.Move` with overwrite)
- `IOException` and `UnauthorizedAccessException` are caught and retried once after 100ms
- FileSystemWatcher on config files triggers reload when another instance modifies them

## Accessibility and Performance

### Accessibility

The extension must meet VS Marketplace accessibility requirements:

- **Keyboard navigation**: All actions in the tool window reachable via keyboard (Tab, Enter, arrow keys, context menu key)
- **Screen reader**: Tree structure with correct UI Automation properties
- **High contrast themes**: Icons and status indicators must work in high contrast — no color-only coding, always include additional symbols (o > x)
- **Focus management**: Sensible focus placement after starting/stopping a task

### Performance

- **Lazy loading**: Projects in the tree are only parsed when the user expands them
- **Caching**: Parse results are cached and only updated on file changes (file watching)
- **Exclusions**: `node_modules`, `.git`, `bin`, `obj`, `.vs` are never scanned
- **Async**: All discovery and parse operations run asynchronously (no UI freeze)
- **Large solutions**: Progress indicator shown for 50+ projects

## Configuration

### Extension Settings

| Setting | Default | Description |
|---|---|---|
| `taskRunnerExtended.autoDiscover` | `true` | Automatically search for task sources |
| `taskRunnerExtended.searchParentDirectories` | `true` | Search parent directories |
| `taskRunnerExtended.maxParentDepth` | `3` | Maximum upward search depth |
| `taskRunnerExtended.newGroupsLocation` | `local` | Where new groups are saved: `local` (task-runner-extended-am.local.json) or `shared` (task-runner-extended-am.json). Both files are always read and merged. |
| `taskRunnerExtended.outputMode` | `auto` | `auto` (terminal if available, else output pane), `outputPane`, or `externalTerminal` |
| `taskRunnerExtended.autoStartGroup` | `""` | Group to auto-start on Solution Open (requires trust) |
| `taskRunnerExtended.confirmOnClose` | `true` | Confirmation dialog when tasks are running on close |
| `taskRunnerExtended.outputEncoding` | `utf-8` | Encoding for task output |
| `taskRunnerExtended.diagnosticLevel` | `Warning` | Logging level for diagnostics pane |
| `taskRunnerExtended.trustedFolders` | `[]` | Folders that can run tasks without trust confirmation |
| `taskRunnerExtended.gracefulShutdownTimeout` | `5000` | Milliseconds until force kill after Ctrl+C signal |
| `taskRunnerExtended.maxPackageJsonDepth` | `20` | Maximum number of package.json files to scan in monorepos |
| `taskRunnerExtended.excludePatterns` | `[]` | Glob patterns to exclude from scanning (e.g., `["**/samples/**", "**/packages/**"]`) |
| `taskRunnerExtended.keepOutputOnStop` | `true` | Keep terminal tab / output pane open after task stops (false = auto-close) |
| `taskRunnerExtended.closeTerminalStopsTask` | `true` | Closing a terminal tab also stops the running task |
| `taskRunnerExtended.maxOutputLines` | `10000` | Maximum lines per task in the output buffer (ring buffer) |

## Technology Stack

| Component | Technology |
|---|---|
| Extension Framework | VisualStudio.Extensibility SDK 17.14.* (Out-of-Process) |
| Target | Visual Studio 2022 17.9+ and Visual Studio 2026 |
| Language | C# / .NET 10 |
| UI Framework | Remote UI (XAML + MVVM, out-of-process) |
| Theming | VS theme integration via `Ardimedia.VsExtensions.Common` (dark/light/blue) |
| JSON Parsing | System.Text.Json |
| YAML Parsing | YamlDotNet (for compose.yml) |
| Process Management | System.Diagnostics.Process + Windows Job Objects |
| Logging | `OutputChannelLogger` from `Ardimedia.VsExtensions.Common` |
| Solution Monitoring | `ToolWindowViewModelBase` from `Ardimedia.VsExtensions.Common` |
| License | MIT (Open Source) |
| Repository | github.com/ardimedia-com/visualstudio-task-runner-extended |

### Project Structure

Based on the established Ardimedia extension patterns (see [visualstudio-binding-redirect-fixer](https://github.com/ardimedia-com/visualstudio-binding-redirect-fixer) and [visualstudio-nuget-fixer](https://github.com/ardimedia-com/visualstudio-nuget-fixer)):

```
visualstudio-task-runner-extended/
+-- src/
|   +-- taskrunnerextended.slnx
|   +-- TaskRunnerExtended/
|   |   +-- TaskRunnerExtended.csproj
|   |   +-- TaskRunnerExtendedExtension.cs        (Extension entry point)
|   |   +-- Commands/
|   |   |   +-- OpenToolWindowCommand.cs           (Tools menu command)
|   |   +-- ToolWindows/
|   |   |   +-- TaskRunnerToolWindow.cs            (ToolWindow, placement: sidebar)
|   |   |   +-- TaskRunnerToolWindowViewModel.cs   (inherits ToolWindowViewModelBase)
|   |   |   +-- TaskRunnerToolWindowControl.cs     (RemoteUserControl)
|   |   |   +-- TaskRunnerToolWindowControl.xaml   (embedded WPF XAML)
|   |   +-- Models/
|   |   |   +-- TaskItem.cs                        (unified task model)
|   |   |   +-- TaskGroup.cs                       (group definition)
|   |   |   +-- TaskSource.cs                      (source file metadata)
|   |   |   +-- TaskStatus.cs                      (idle/running/error enum)
|   |   +-- Services/
|   |   |   +-- Discovery/
|   |   |   |   +-- ITaskDiscoverer.cs             (interface per source type)
|   |   |   |   +-- TasksJsonDiscoverer.cs
|   |   |   |   +-- PackageJsonDiscoverer.cs
|   |   |   |   +-- CsprojDiscoverer.cs
|   |   |   |   +-- LaunchSettingsDiscoverer.cs
|   |   |   |   +-- TasksVsJsonDiscoverer.cs
|   |   |   |   +-- GruntfileDiscoverer.cs
|   |   |   |   +-- GulpfileDiscoverer.cs
|   |   |   |   +-- ComposeYmlDiscoverer.cs
|   |   |   +-- Execution/
|   |   |   |   +-- TaskRunner.cs                  (process management, Job Objects)
|   |   |   |   +-- ProcessTreeManager.cs          (kill tree, graceful shutdown)
|   |   |   +-- FileWatcherService.cs              (FileSystemWatcher, debounce)
|   |   |   +-- GroupConfigService.cs              (read/write task-runner-extended-am.json)
|   |   |   +-- VariableResolver.cs                (${workspaceFolder} etc.)
|   |   |   +-- MsBuildPathResolver.cs             (vswhere.exe for .NET Framework)
|   |   +-- Images/
|   |   |   +-- TaskRunnerExtended.*.png           (multiple sizes)
|   |   +-- .vsextension/
|   |       +-- string-resources.json
|   +-- TaskRunnerExtended.Tests/
|       +-- TaskRunnerExtended.Tests.csproj
|       +-- Discovery/                             (unit tests per discoverer)
|       +-- Execution/                             (integration tests)
+-- .github/
|   +-- workflows/
|   |   +-- build.yml
|   |   +-- release.yml
|   +-- publishManifest.json
+-- .claude/
    +-- feature.md
    +-- reference-cli.md
    +-- reference-task-config-files.md
```

### Key Patterns (from Template Projects)

- **Extension entry point**: Class inherits `Extension`, decorated with `[VisualStudioContribution]`
- **Commands**: Inherit from `Command`, placed via `CommandPlacement.KnownPlacements.ToolsMenu`
- **Tool Window**: Inherits from `ToolWindow`, returns `IRemoteUserControl` from `GetContentAsync`
- **Tool Window Placement**: **Sidebar** (same area as Solution Explorer, Git Changes, GitHub Copilot Chat) — unlike the template projects which use `DocumentWell`
- **ViewModel**: Inherits from `ToolWindowViewModelBase` (common library), uses `[DataContract]` for Remote UI serialization
- **XAML**: Embedded resource, uses `platformui:EnvironmentColors` for VS theming
- **Logging**: `OutputChannelLogger` from `Ardimedia.VsExtensions.Common`
- **Solution monitoring**: Built-in debounced fingerprint detection from base class
- **NuGet dependency**: `Ardimedia.VsExtensions.Common` v1.0.* for shared infrastructure
- **CI/CD**: GitHub Actions — build on push/PR, release on v* tags, publish to VS Marketplace
- **Testing**: MSTest, category filtering (`TestCategory=Unit`), ProjectReference to main extension

### Implementation Patterns (from Template Projects)

These patterns are proven in the existing Ardimedia extensions and should be reused:

**Parallel Discovery:**
- Use `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 5` for scanning multiple source files
- `Interlocked.Increment` for progress counters, `lock` or `ConcurrentDictionary` for result aggregation
- `CancellationToken` propagated through all async methods
- `ConfigureAwait(false)` on all background operations

**Logging:**
- `OutputChannelLogger` from common library — fire-and-forget via `_ = Task.Run()`
- Lazy channel creation on first write
- Silent exception handling (logging must never crash the extension)

**UI Updates from Background Threads:**
- No `Dispatcher.Invoke` needed — Remote UI automatically marshals property changes to UI thread
- Property setters can be called directly from `Task.Run` background threads
- `ObservableList<T>` for collections bound to the UI

**Settings Storage:**
- Extension settings (outputMode, diagnosticLevel, etc.) stored in `%LOCALAPPDATA%\TaskRunnerExtended\settings.json`
- JSON format with `System.Text.Json`, `WriteIndented = true`
- Best-effort save/load: silent fallback to defaults on corruption
- Task group config files (`task-runner-extended-am.json`, `task-runner-extended-am.local.json`) stored in project/solution root (not in LOCALAPPDATA)

**Solution Detection:**
- `Extensibility.Workspaces().QueryProjectsAsync()` to get project paths
- Fingerprint = sorted project paths joined with `|`
- Two-pass debounce (5s interval, 2 stable readings required) to avoid false triggers during solution load
- Solution path inferred from common root of project paths

**Debouncing:**
- `CancellationTokenSource` pattern: each new event cancels the previous timer
- `Task.Delay(ms, token)` with `OperationCanceledException` catch
- Used for: file watcher refresh (500ms), filter input (1500ms in templates)

## Phases

### Phase 1: Foundation

Focus: Project setup, critical path validation, then tasks.json + .csproj discovery and task execution.

**Step 1 — Project setup:**
- Create solution, extension project, test project (based on template projects)
- Set up CI/CD (build.yml, release.yml)
- Reference `Ardimedia.VsExtensions.Common`

**Step 2 — Critical path validation (spike):**

All five questions below must be answered before building features. Results determine the UI architecture and output strategy.

1. **Remote UI: TreeView support?**
   - Can Remote UI render a `TreeView` with `HierarchicalDataTemplate`?
   - If not: Alternative with nested `Expander`/`ItemsControl` constructs
   - The template projects only use flat `ItemsControl`/`ListView` — TreeView is unproven

2. **Remote UI: Drag-and-Drop?**
   - Can items be dragged between nodes in Remote UI?
   - Likely NOT feasible (RPC-based rendering can't handle continuous mouse events)
   - If not: Alternative via context menu "Add to Group..." with selection dialog

3. **Remote UI: Context Menus?**
   - Does WPF `ContextMenu` work in Remote UI?
   - If not: Alternative via toolbar buttons in the tool window header

4. **Tool Window: Sidebar Placement?**
   - Test `ToolWindowPlacement.DockedTo(SolutionExplorerGuid)` with `DockDirection`
   - No `ToolWindowPlacement.Sidebar` exists — docking alongside Solution Explorer requires its GUID (`3AE79031-E1BC-11D0-8F78-00A0C9110057`)
   - Fallback: `DocumentWell` (like the template projects)

5. **VS Internal Terminal API?**
   - Try VSSDK COM interfaces (`IVsTerminalService` or similar)
   - Try DTE automation (`DTE.ExecuteCommand`)
   - Try mixed-mode (out-of-process extension with in-process VSSDK access)
   - If not feasible: Output Window Panes become the primary output strategy

**Step 3 — Core features:**
- Read and parse .vscode/tasks.json (label, command, args, cwd, isBackground)
- Discover .csproj MSBuild targets (both SDK-style and .NET Framework)
  - SDK detection via `Sdk` attribute
  - .NET Framework: `msbuild.exe` path resolution via `vswhere.exe`
  - Recognize both `BeforeTargets="Build"` and `BeforeBuild` naming patterns
- Tool window with unified tree view (Available Configuration Files root node)
- Start task (output in VS terminal or Output Window Pane depending on spike result)
- Stop task (entire process tree via Job Objects)
- Status tracking (idle / running / error)
- Basic hierarchy (solution directory + project directories)
- `${workspaceFolder}` variable resolution
- File watching on discovered source files (debounced refresh)
- Diagnostic logging (Output Pane)
- Error handling (malformed JSON, missing commands, parse errors)

**Not in Phase 1**: Compound tasks, additional task sources, grouping, extended variable resolution.

### Phase 2: Multi-Source

All remaining task sources and compound tasks.

- Discover package.json npm/pnpm/yarn scripts (package manager detection via lock files)
- Read tasks.vs.json
- Discover Gruntfile.js tasks
- Discover gulpfile.js tasks
- Discover compose.yml services
- Read launchSettings.json profiles
- Extended variable resolution (`${file}`, `${env:...}`)
- Compound tasks (`dependsOn`, `dependsOrder`: parallel/sequence)
- Hierarchy discovery upward (parent directories, configurable depth)
- Circular dependency detection

### Phase 3: Grouping

Task groups and start profiles.

- Task groups data model (task-runner-extended-am.json / task-runner-extended-am.local.json)
- Run Groups root node in unified tree view
- Start/stop per group via double-click or context menu (parallel/sequential)
- Drag-and-drop from Available Configuration Files root node into groups (or context menu "Add to Group..." if drag-drop not feasible)
- Workspace trust concept (confirmation on first start)
- autoStartGroup with trust confirmation

### Phase 4: Advanced Output + Remote

- Problem matcher support (extract errors from task output)
- ANSI color support (if using Output Window Pane fallback)
- Remote/SSH/WSL basics (task execution on remote hosts)

### Phase 5: Polish and Marketplace

- Settings UI (visual configuration)
- Keyboard shortcuts (Ctrl+Shift+B -> Default Build Group, tool window navigation)
- Accessibility audit (keyboard navigation, screen reader, high contrast)
- Conversion tasks.json <-> tasks.vs.json (optional)
- JSON editor with IntelliSense for tasks.json (optional)
- Onboarding / first-run experience (walkthrough, empty state with hints)
- Documentation + README
- VS Marketplace publication

## Scope

### What This Extension is NOT

- Not a replacement for MSBuild / dotnet CLI
- Not a full VS Code emulator
- Not a CI/CD tool (local development only)
- Not a package manager (npm/NuGet installation)

### Relationship to Existing VS Features

The extension **replaces** the functionality of the Task Runner Explorer by discovering the same sources (Grunt, Gulp, npm via package.json) plus many more (tasks.json, .csproj, launchSettings.json, compose.yml, tasks.vs.json) in a unified view with grouping and compound task support.

For a detailed comparison of built-in VS launch features vs this extension, see [VS Launch Functions in reference-cli.md](reference-cli.md).

| VS Feature | What it offers | What this extension adds |
|---|---|---|
| Task Runner Explorer | Grunt/Gulp/npm discovery, build bindings | Unified view, 8 sources, grouping, compound tasks |
| Multiple Startup Projects | Start multiple .NET projects | Start any command (npm, docker, dotnet, custom) |
| Launch Profile Dropdown | Switch launchSettings.json profiles | Task groups as richer alternative |
| Pre/Post Build Events | Single command per build event | Multiple tasks, independent of build lifecycle |

## Technical Risks

| Risk | Severity | Mitigation |
|---|---|---|
| **Remote UI: TreeView + Drag-and-Drop** | **HIGH** | Remote UI supports a subset of WPF. TreeView and Drag-and-Drop are unproven — template projects only use flat lists. **Validate in Phase 1 spike.** Fallback: nested Expander/ItemsControl for tree, context menu "Add to Group..." dialog for grouping. |
| **VS Internal Terminal API** | **HIGH** | No official API in VisualStudio.Extensibility SDK. DTE automation can open terminal but NOT create/name/control tabs. VSSDK COM requires mixed-mode. **Validate in Phase 1 spike.** Likely outcome: Output Window Panes as primary approach. |
| **Remote UI: Context Menus** | **MEDIUM-HIGH** | WPF ContextMenu may not work in Remote UI. Template projects don't use them. **Validate in Phase 1 spike.** Fallback: toolbar buttons in tool window header. |
| **Sidebar Placement** | **MEDIUM** | No `ToolWindowPlacement.Sidebar` exists. Must use `DockedTo(SolutionExplorerGuid)`. Only works if Solution Explorer is open. Fallback: DocumentWell. **Validate in Phase 1 spike.** |
| Process tree management on Windows | MEDIUM-HIGH | Windows Job Objects with `KILL_ON_JOB_CLOSE`. Gotcha: If extension host crashes, handle may be lost before kernel resolves Job Object. Test crash scenarios. |
| **Console encoding (Windows)** | **MEDIUM** | `cmd.exe` defaults to CP437/CP1252, not UTF-8. Must set `Process.StartInfo.StandardOutputEncoding = Encoding.UTF8` or prepend `chcp 65001`. Without this, umlauts and special chars are corrupted. |
| **FileSystemWatcher reliability** | **MEDIUM** | Buffer overflow on mass file changes (e.g., `npm install`). Set `InternalBufferSize` to 64KB+. Handle duplicate events. Unreliable on network shares (SMB). Limit watcher count for large solutions. |
| **Memory: long-running background tasks** | **MEDIUM** | Background tasks (watchcss, docker compose) run for hours. Output buffer grows unbounded. Implement ring buffer (max 10'000 lines). Ensure Process/Job Object handles are properly disposed. |
| **Concurrent config file access** | **MEDIUM** | Multiple VS instances can open the same solution. Concurrent read/write on task-runner-extended-am.json can corrupt data. Use atomic writes (temp file + File.Move). Watch config file for external changes. |
| **Remote UI: real-time updates over RPC** | **MEDIUM** | Every property change is an RPC roundtrip. Many concurrent tasks with fast output could cause UI lag. Throttle status updates (max 10/sec per task). |
| **Extension update kills running tasks** | **LOW-MEDIUM** | VS can update VSIX in background. Extension host restart terminates Job Objects, killing all running tasks without warning. Document as known limitation. |
| tasks.json compatibility | MEDIUM | Clearly document which subset is supported. Ignore unknown properties. Tasks with `${input:...}` variables are marked as "not startable" with clear error message. |
| Grunt/Gulp task discovery | LOW | Decided: Shell out via `gulp --tasks-json` / `grunt --help`. Requires installed CLI + node_modules. |
| .csproj parsing complexity | LOW | Top-level targets with Exec in the .csproj only, no imports. |
| compose.yml parsing | LOW | YAML parsing is straightforward. Only read `services` section. |
