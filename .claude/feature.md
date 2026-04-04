---
status: Stable
updated: 2026-04-04 22:00h
references:
  - .claude/reference-cli.md — CLI reference (dotnet, npm, docker, VS launch functions)
  - .claude/reference-task-config-files.md — Task configuration file formats
---

# Visual Studio Task Runner Extended

## Vision

A Visual Studio extension that discovers task definitions from multiple sources, displays them in a unified view, and allows grouping and parallel execution — directly in Visual Studio. Bridges the gap between VS Code (tasks.json with compound tasks) and Visual Studio (no equivalent).

## Task Sources

The extension discovers tasks from **8 sources**. For detailed file format documentation, see [reference-task-config-files.md](reference-task-config-files.md).

| Source | What it discovers |
|---|---|
| `.vscode/tasks.json` | VS Code tasks (label, command, args, cwd, dependsOn, problemMatcher). Not supported: `input` variables, `runOptions`. |
| `.vs/tasks.vs.json` | VS native tasks for Open Folder mode (taskLabel, command, appliesTo) |
| `package.json` | npm/pnpm/yarn scripts. Package manager detected via lock files (pnpm > yarn > npm). Background heuristic: name contains watch/dev/serve. |
| `*.csproj` | Custom `<Target>` with `<Exec>`. SDK-style (dotnet msbuild) and non-SDK-style (msbuild.exe via vswhere). Top-level targets only. |
| `launchSettings.json` | .NET launch profiles (Project, Executable, Docker) with environment variables |
| `Gruntfile.js` | Tasks via `grunt --help --no-color` (requires grunt-cli + node_modules) |
| `gulpfile.js` | Tasks via `gulp --tasks-json` (requires gulp-cli + node_modules) |
| `compose.yml` | Docker Compose services (individual + all services) |

## Task Groups

Groups are stored in two separate files in the project/solution root:

- `task-runner-extended-am.json` — **Shared (commited)**, for the team via git
- `task-runner-extended-am.local.json` — **Local (not commited)**, per-user settings

The `am` prefix (ardimedia) prevents naming collisions with other extensions.

Groups are displayed under separate file nodes in the tree. Users choose shared or local when creating groups or adding tasks to groups.

```json
{
  "version": "1.0",
  "groups": [
    {
      "name": "Development",
      "tasks": [
        { "source": ".vscode/tasks.json", "task": "watchcss", "startOrder": "parallel" },
        { "source": ".vscode/tasks.json", "task": "dotnet-watch", "startOrder": "parallel" }
      ]
    }
  ]
}
```

## Tool Window

Tree structure with three toolbar tabs (Tasks, Background, Feedback):

```
Available Configuration Files (Tasks)
  .vscode/tasks.json
    watchcss
    dotnet-watch
    dev (compound: watchcss + dotnet-watch)
  Bvd.Li.Web.Ui.Blazor/package.json
    npm: buildcss
    npm: watchcss (background)
  Bvd.Li.Web.Ui.Blazor/Bvd.Li.Web.Ui.Blazor.csproj
    msbuild: BuildTailwindCss (Pre-Build, Debug)

Run Groups
  Shared (commited)
    Development
      watchcss (parallel)
      dotnet-watch (parallel)
  Local (not commited)
```

### Context Menu

| Node Type | Start | Stop | Add to Group | Add Group | Remove from Group | Rename Group | Delete Group |
|---|---|---|---|---|---|---|---|
| Source file | Yes | Yes | - | - | - | - | - |
| Task | Yes | Yes | Yes | - | - | - | - |
| Shared/Local file node | - | - | - | Yes | - | - | - |
| Group | Yes | Yes | - | - | - | Yes | Yes |
| Group entry | Yes | Yes | - | - | Yes | - | - |

Start/Stop on source files starts/stops ALL tasks from that file.

### Details Pane

Clicking a task shows command, working directory, type, and status in a bottom pane with selectable/copyable text.

## Process Management

- **Shell mode** (`cmd.exe /c`): default for tasks.json, npm scripts, compose
- **Process mode**: direct execution without shell
- **Job Objects**: each task process is placed in a Windows Job Object for reliable process tree kill
- **Graceful shutdown**: Ctrl+C signal first, force kill after 5 seconds
- **Output**: dedicated Output Window Pane per task with real-time streaming, ANSI stripping
- **Problem Matcher**: detects errors/warnings in output (MSBuild, TypeScript, npm patterns)

### Task Type Classification

| Type | Behavior | Example |
|---|---|---|
| Normal | Starts, runs, exits on its own | `dotnet build`, `npm run buildcss` |
| Background | Runs indefinitely until stopped | `dotnet watch`, `npm run watchcss` |

### Compound Tasks

- `dependsOrder: "parallel"` — start all dependent tasks simultaneously
- `dependsOrder: "sequence"` — start one after another, next starts after exit code 0
- Background tasks in sequence: next task starts immediately (no waiting)

## File Watching

- `FileSystemWatcher` on all source files + group config files
- 500ms debounce to avoid rapid rescans
- Running tasks are NOT automatically restarted on file changes

## Not Yet Implemented (Roadmap)

- Extension settings UI (autoDiscover, maxParentDepth, autoStartGroup, etc.)
- Workspace trust / first-run confirmation for untrusted repositories
- Keyboard shortcuts (Ctrl+Shift+B for default build group)
- External terminal mode for interactive tasks (stdin)
- Output buffer ring buffer (max lines per task)
- Solution filter (.slnf) awareness

## Scope

### What This Extension is NOT

- Not a replacement for MSBuild / dotnet CLI
- Not a full VS Code emulator
- Not a CI/CD tool (local development only)
- Not a package manager

### Relationship to Existing VS Features

| VS Feature | What it offers | What this extension adds |
|---|---|---|
| Task Runner Explorer | Grunt/Gulp/npm discovery | Unified view, 8 sources, grouping, compound tasks |
| Multiple Startup Projects | Start multiple .NET projects | Start any command (npm, docker, dotnet, custom) |
| Launch Profile Dropdown | Switch launchSettings.json profiles | Task groups as richer alternative |
| Pre/Post Build Events | Single command per build event | Multiple tasks, independent of build lifecycle |

## Technology Stack

| Component | Technology |
|---|---|
| Extension Framework | VisualStudio.Extensibility SDK 17.14.* (Out-of-Process) |
| Target | Visual Studio 2022 17.14+ and Visual Studio 2026 |
| Language | C# / .NET 10 |
| UI Framework | Remote UI (XAML + MVVM) |
| JSON Parsing | System.Text.Json |
| YAML Parsing | YamlDotNet |
| Process Management | System.Diagnostics.Process + Windows Job Objects |
| Common Library | Ardimedia.VsExtensions.Common |
| License | MIT |

## Known Limitations

- VS Internal Terminal API is not accessible from OOP extensions — Output Window Panes used instead
- Drag-and-Drop not possible in Remote UI — "Add to Group..." context menu used instead
- `ToggleCommand.IsChecked` initial state not rendered visually by VS for OOP toolbar buttons
