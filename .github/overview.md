# Task Runner Extended

Discovers tasks from multiple sources, displays them in a unified tree view, and allows grouping and parallel execution -- directly in Visual Studio.

Bridges the gap between VS Code (tasks.json with compound tasks) and Visual Studio (no equivalent).

## The Problem

Visual Studio offers no way to start multiple background processes (e.g., `npm run watchcss` + `dotnet watch` + `docker compose up`) in a coordinated manner. Developers must manually open terminals, remember which processes need to run, and manage tasks from different tools in different locations.

## Features

- **Task Discovery** -- discovers tasks from `.vscode/tasks.json`, `tasks.vs.json`, `package.json`, `.csproj` (MSBuild targets), `launchSettings.json`, and `compose.yml`
- **Unified Tree View** -- all tasks from all sources in a single sidebar tool window
- **Run Groups** -- bundle tasks into groups and start them with one click
- **Parallel + Sequential** -- start tasks in parallel or in sequence with dependency ordering
- **Compound Tasks** -- VS Code-style `dependsOn` with `dependsOrder` support
- **Background Task Management** -- see which processes are running, stop them individually or as a group
- **Details Pane** -- click any task to see command, working directory, type, and status
- **Toolbar Tabs** -- switch between Tasks, Background (extension info), and Feedback (GitHub issue form)
- **Problem Matcher** -- detects errors and warnings in task output
- **.NET Framework Support** -- both SDK-style and non-SDK-style `.csproj` projects
- **Package Manager Detection** -- auto-detects npm, pnpm, or yarn via lock files
- **File Watching** -- automatically refreshes when task source files change
- **Process Tree Kill** -- Windows Job Objects ensure all child processes are terminated

## Task Sources

| Source | What it discovers |
|---|---|
| `.vscode/tasks.json` | VS Code task definitions (labels, commands, compound tasks) |
| `.vs/tasks.vs.json` | VS native tasks for Open Folder mode |
| `package.json` | npm/pnpm/yarn scripts |
| `.csproj` | Custom MSBuild targets with `<Exec>` commands |
| `launchSettings.json` | .NET launch profiles |
| `compose.yml` | Docker Compose services |

## Usage

1. Open a solution or folder in Visual Studio
2. Open the tool window: **Tools** > **Task Runner Extended**
3. The tree shows all discovered tasks grouped by source file
4. **Right-click** a task for options: Start, Stop, Add to Group
5. Create **Run Groups** via "Add to Group..." context menu

## Run Groups

Run Groups bundle multiple tasks that should be started together. Groups are organized under two file nodes:

- **Shared (commited)** -- `task-runner-extended-am.json`, shared with the team via git
- **Local (not commited)** -- `task-runner-extended-am.local.json`, personal per-user settings

To create a group:

- Right-click a task > **Add to Group...**
- Choose **Shared** or **Local** as the target file
- Enter a group name (default: "Development")
- Right-click the group > **Start** to start all tasks

Add `*.local.json` to your `.gitignore` to prevent local settings from being committed.

## Requirements

- Visual Studio 2022 (17.9+) or Visual Studio 2026
- .NET 10 runtime
