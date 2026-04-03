# Task Runner Extended

[![Build](https://github.com/ardimedia-com/visualstudio-task-runner-extended/actions/workflows/build.yml/badge.svg)](https://github.com/ardimedia-com/visualstudio-task-runner-extended/actions/workflows/build.yml)

A Visual Studio 2022/2026 extension that discovers tasks from 8 sources, displays them in a unified tree view, and allows grouping and parallel execution -- directly in Visual Studio.

> **Note:** This extension is in early development (Phase 1). If you are interested, please [open an issue](https://github.com/ardimedia-com/visualstudio-task-runner-extended/issues) to let us know.

## The Problem

Visual Studio offers no way to start multiple background processes (e.g., `npm run watchcss` + `dotnet watch` + `docker compose up`) in a coordinated manner. Developers must manually open terminals, remember which processes need to run, and manage tasks from different tools in different locations.

This extension solves that with a unified task view and one-click task groups.

## Features

- **8 Task Sources** -- discovers tasks from `.vscode/tasks.json`, `tasks.vs.json`, `package.json`, `.csproj` (MSBuild targets), `launchSettings.json`, `Gruntfile.js`, `gulpfile.js`, and `compose.yml`
- **Unified Tree View** -- all tasks from all sources in a single sidebar tool window
- **Run Groups** -- bundle tasks into groups (e.g., "Development" = docker db + css watcher + dotnet watch) and start them with one click
- **Parallel + Sequential** -- start tasks in parallel or in sequence with dependency ordering
- **Compound Tasks** -- VS Code-style `dependsOn` with `dependsOrder` support
- **Background Task Management** -- see which processes are running, stop them individually or as a group
- **.NET Framework Support** -- both SDK-style and non-SDK-style `.csproj` projects
- **Package Manager Detection** -- auto-detects npm, pnpm, or yarn via lock files
- **File Watching** -- automatically refreshes when task source files change
- **Solution Filter Support** -- respects `.slnf` filters (only shows loaded projects)
- **Workspace Trust** -- confirmation before executing commands from untrusted repositories
- **Theme-Aware** -- fully adapts to Light, Dark, Blue, and High Contrast themes
- **Solution Monitor** -- automatically scans when a solution is opened or changed

## Task Sources

| Source | What it discovers |
|---|---|
| `.vscode/tasks.json` | VS Code task definitions (labels, commands, compound tasks) |
| `.vs/tasks.vs.json` | VS native tasks for Open Folder mode |
| `package.json` | npm/pnpm/yarn scripts |
| `.csproj` | Custom MSBuild targets with `<Exec>` commands |
| `launchSettings.json` | .NET launch profiles |
| `Gruntfile.js` | Grunt tasks (via `grunt --help`) |
| `gulpfile.js` | Gulp tasks (via `gulp --tasks-json`) |
| `compose.yml` | Docker Compose services |

## Installation

### From VSIX File

1. Download the `.vsix` file from [Releases](https://github.com/ardimedia-com/visualstudio-task-runner-extended/releases)
2. Double-click the file to install, or use **Extensions** > **Manage Extensions** > **Install from File**

## Usage

1. Open a solution or folder in Visual Studio
2. Open the tool window: **Tools** > **Task Runner Extended**
3. The tree shows all discovered tasks grouped by source file
4. **Double-click** a task to start it, or **right-click** for more options
5. Create **Run Groups** by dragging tasks or using the context menu "Add to Group..."

## Configuration

Task groups are stored in two files (both in the project/solution root):

| File | Purpose | In Git? |
|---|---|---|
| `task-runner-extended-am.json` | Shared with team | Yes |
| `task-runner-extended-am.local.json` | Per-user settings | No (gitignored via `*.local.json`) |

## Requirements

- Visual Studio 2022 (17.9+) or Visual Studio 2026
- .NET 10 runtime

## License

[MIT](LICENSE)
