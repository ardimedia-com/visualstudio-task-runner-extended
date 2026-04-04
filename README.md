# Task Runner Extended

[![Build](https://github.com/ardimedia-com/visualstudio-task-runner-extended/actions/workflows/build.yml/badge.svg)](https://github.com/ardimedia-com/visualstudio-task-runner-extended/actions/workflows/build.yml)
[![Release](https://github.com/ardimedia-com/visualstudio-task-runner-extended/actions/workflows/release.yml/badge.svg)](https://github.com/ardimedia-com/visualstudio-task-runner-extended/actions/workflows/release.yml)
[![Visual Studio Marketplace](https://img.shields.io/visual-studio-marketplace/v/Ardimedia.TaskRunnerExtended.svg)](https://marketplace.visualstudio.com/items?itemName=Ardimedia.TaskRunnerExtended)
[![Visual Studio Marketplace Downloads](https://img.shields.io/visual-studio-marketplace/d/Ardimedia.TaskRunnerExtended.svg)](https://marketplace.visualstudio.com/items?itemName=Ardimedia.TaskRunnerExtended)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A Visual Studio 2022/2026 extension that discovers tasks from multiple sources, displays them in a unified tree view, and allows grouping and parallel execution -- directly in Visual Studio.

Bridges the gap between VS Code (tasks.json with compound tasks) and Visual Studio (no equivalent).

## The Problem

Visual Studio offers no way to start multiple background processes (e.g., `npm run watchcss` + `dotnet watch` + `docker compose up`) in a coordinated manner. Developers must manually open terminals, remember which processes need to run, and manage tasks from different tools in different locations.

This extension solves that with a unified task view and one-click run groups.

## Features

- **Task Discovery** -- discovers tasks from `.vscode/tasks.json`, `tasks.vs.json`, `package.json`, `.csproj` (MSBuild targets), `launchSettings.json`, and `compose.yml`
- **Unified Tree View** -- all tasks from all sources in a single sidebar tool window (docked next to Solution Explorer)
- **Run Groups** -- bundle tasks into groups (e.g., "Development" = docker db + css watcher + dotnet watch) and start them with one click
- **Parallel + Sequential** -- start tasks in parallel or in sequence with dependency ordering
- **Compound Tasks** -- VS Code-style `dependsOn` with `dependsOrder` support
- **Background Task Management** -- see which processes are running (icon changes), stop them individually or as a group
- **Problem Matcher** -- detects errors and warnings in task output (MSBuild, TypeScript, npm patterns)
- **.NET Framework Support** -- both SDK-style and non-SDK-style `.csproj` projects
- **Package Manager Detection** -- auto-detects npm, pnpm, or yarn via lock files (priority: pnpm > yarn > npm)
- **File Watching** -- automatically refreshes when task source files change (500ms debounce)
- **VS KnownMoniker Icons** -- native VS icons that adapt to all themes (Light, Dark, Blue, High Contrast)
- **Solution Monitor** -- automatically scans when a solution is opened or changed
- **Toolbar** -- Refresh, Stop All, Collapse All buttons in the tool window header
- **Context Menu** -- Start, Stop, Add to Group, Rename Group, Delete Group, Remove from Group
- **VS User Prompts** -- input prompts for group names, confirmation for delete
- **Output Window Pane** -- dedicated output pane per task with real-time streaming
- **Process Tree Kill** -- Windows Job Objects ensure all child processes are terminated
- **ANSI Strip** -- removes color codes from output, sets NO_COLOR environment variable

## Task Sources

| Source | What it discovers |
|---|---|
| `.vscode/tasks.json` | VS Code task definitions (labels, commands, compound tasks) |
| `.vs/tasks.vs.json` | VS native tasks for Open Folder mode |
| `package.json` | npm/pnpm/yarn scripts (with background task heuristics) |
| `.csproj` | Custom MSBuild targets with `<Exec>` commands (SDK + .NET Framework) |
| `launchSettings.json` | .NET launch profiles (Project, Executable, Docker) |
| `compose.yml` | Docker Compose services (individual + all services) |

## Installation

### From VSIX File

1. Download the `.vsix` file from [Releases](https://github.com/ardimedia-com/visualstudio-task-runner-extended/releases)
2. Double-click the file to install, or use **Extensions** > **Manage Extensions** > **Install from File**

## Usage

1. Open a solution or folder in Visual Studio
2. Open the tool window: **Tools** > **Task Runner Extended**
3. The tree shows all discovered tasks grouped by source file
4. **Right-click** a task for options: Start, Stop, Add to Group
5. Create **Run Groups** via "Add to Group..." or the "+ New Group..." node
6. **Right-click** a group to: Start All, Stop All, Rename, Delete
7. Use the **toolbar** buttons for Refresh, Stop All, Collapse All

### Run Groups

Run Groups bundle multiple tasks that should be started together:

1. Right-click a task > **Add to Group...**
2. Enter a group name (default: "Development") or use an existing one
3. The group appears under **Run Groups** in the tree
4. Right-click the group > **Start** to start all tasks in the group

Groups are stored in two files (both in the project/solution root):

| File | Purpose | In Git? |
|---|---|---|
| `task-runner-extended-am.json` | Shared with team | Yes |
| `task-runner-extended-am.local.json` | Per-user settings | No (gitignored via `*.local.json`) |

Local groups override shared groups with the same name.

## Technical Details

- **Extension Framework**: VisualStudio.Extensibility SDK 17.14 (Out-of-Process)
- **UI**: Remote UI (XAML + MVVM) with custom TreeView ControlTemplate
- **Process Management**: Windows Job Objects with graceful shutdown (Ctrl+C, then force kill after 5s)
- **Shared Library**: [Ardimedia.VsExtensions.Common](https://github.com/ardimedia-com/visualstudio-vs-extensions-common) for theming, solution monitoring, and output logging

## Requirements

- Visual Studio 2022 (17.9+) or Visual Studio 2026
- .NET 10 runtime

## Contributing

Contributions are welcome! Please [open an issue](https://github.com/ardimedia-com/visualstudio-task-runner-extended/issues) first to discuss what you would like to change.

## License

[MIT](LICENSE)
