---
status: Stable
updated: 2026-04-03 21:15h
references:
  - .claude/feature.md — Main feature specification
  - .claude/reference-task-config-files.md — Task configuration file formats
---

# Reference: CLI Commands and Visual Studio Launch Functions

Documents the CLI tools and commands that are typically executed as tasks. For the configuration files that define these tasks, see [reference-task-config-files.md](reference-task-config-files.md).

## Overview: Task Type Detection

| CLI Command | Normal Task | Background Task |
|---|---|---|
| **dotnet** | | |
| `dotnet run` | Yes | |
| `dotnet watch` | | Yes |
| `dotnet build` | Yes | |
| `dotnet clean` | Yes | |
| `dotnet restore` | Yes | |
| `dotnet test` | Yes | |
| `dotnet publish` | Yes | |
| `dotnet ef` | Yes | |
| `dotnet msbuild -t:X` | Yes | |
| `msbuild.exe -t:X` | Yes | |
| **Node.js** | | |
| `npm run <script>` | Without --watch | With --watch |
| `pnpm run <script>` | Without --watch | With --watch |
| `yarn run <script>` | Without --watch | With --watch |
| `npx <tool>` | Without --watch | With --watch |
| `tsc` | Without --watch | With --watch |
| `tailwindcss` | Without --watch | With --watch |
| **Build tools** | | |
| `grunt <task>` | Yes | With `watch` task |
| `gulp <task>` | Yes | With `watch` task |
| **Docker** | | |
| `docker compose up -d` | Yes | |
| `docker compose up` | | Yes |

## Visual Studio Launch Functions

### Default Launch Functions (built-in)

These are the standard ways to build and launch projects in Visual Studio. They all operate on the **startup project** (or multiple startup projects) and use the selected **launch profile** from launchSettings.json.

| Action | Shortcut | What happens |
|---|---|---|
| **Start Debugging** | F5 | Build → Debugger attached → App starts via `dotnet run` |
| **Start Without Debugging** | Ctrl+F5 | Build → App starts via `dotnet run` (no debugger, faster) |
| **Build Solution** | Ctrl+Shift+B | Build only, no launch |
| **Rebuild Solution** | Ctrl+Alt+F7 | Clean + Build |
| **Start Without Building** | — (menu only) | App starts without prior build |
| **Attach to Process** | Ctrl+Alt+P | Attach debugger to running process |
| **Profile** | Alt+F2 | Start Performance Profiler |

**Note:** Custom MSBuild targets with `<Exec>` in the .csproj (e.g., `BeforeTargets="Build"`) do run as part of these functions — so an npm watcher or Tailwind compiler *can* be triggered via F5/Build. However, they run as part of the build lifecycle, not as independently managed processes. There is no visibility into their status, no way to stop them individually, and no grouping.

### Other Launch Possibilities (built-in)

Visual Studio offers additional mechanisms that go beyond single-project launch:

| Feature | Access | What it does |
|---|---|---|
| **Launch Profile Dropdown** | Toolbar | Switch between profiles from launchSettings.json (e.g., "https", "dotnet watch", "Docker") |
| **Multiple Startup Projects** | Solution Properties → Startup Project | Start multiple projects simultaneously (e.g., API + Web frontend) |
| **Task Runner Explorer** | Right-click → Task Runner Explorer | Run npm/Grunt/Gulp tasks, bind them to build events |
| **External Tools** | Tools → External Tools | Define custom commands (can show output in Output Window, but no task management or grouping) |
| **Pre/Post Build Events** | Project Properties → Build Events | Run shell commands before/after build (limited to one command per event) |

**Limitations:** 
- "Multiple Startup Projects" only works for .NET projects in the solution — not for arbitrary commands like `npm run watchcss`
- Task Runner Explorer discovers npm/Grunt/Gulp tasks but has no grouping, no compound tasks, and no unified view across task sources
- Pre/Post Build Events are single commands tied to the build lifecycle — they can't be started independently or grouped

### Task Runner Extended Launch Features

The Task Runner Extended extension fills the gaps left by the built-in features:

| Feature | What it adds |
|---|---|
| **Unified task view** | All tasks from tasks.json, tasks.vs.json, package.json, .csproj, launchSettings.json in one tree |
| **Task groups** | Bundle multiple tasks (npm watcher + dotnet watch + docker compose) into a single start profile |
| **One-click start** | Double-click a group in the tree to start all its tasks |
| **Parallel + sequential** | Start tasks in parallel or in sequence (with dependency ordering) |
| **Compound tasks** | VS Code-style `dependsOn` with `dependsOrder` — not available natively in VS |
| **Background task management** | See which processes are running, stop them individually or as a group |
| **Cross-tool coordination** | Start Docker containers, then CSS watcher, then dotnet watch — all coordinated |
| **Auto-start on Solution Open** | Optionally start a task group when the solution opens (with workspace trust) |

**Example workflow without the extension:**
1. Open terminal → `docker compose up -d` (start database)
2. Open terminal → `npm run watchcss` (start Tailwind watcher)
3. Press F5 or run `dotnet watch` (start the app)
4. Remember to stop all three when done

**Same workflow with Task Runner Extended:**
1. Double-click "Development" group in the Task Runner Extended tree → all three start automatically

## .NET CLI

### dotnet run

```
dotnet run --project MyApp.csproj
```

- Runs `dotnet build`, then starts the app
- One-time start, no file watching
- Exits when the app exits
- `--no-build` skips the build (if already compiled)
- `--launch-profile https` selects a profile from launchSettings.json
- This is what VS executes internally for F5/Ctrl+F5 (`commandName: "Project"`)

Task type: **Normal task** (exits with the app).

### dotnet watch

```
dotnet watch --project MyApp.csproj
```

- Wrapper around `dotnet run` with file watching
- Watches **all** `.cs`, `.razor`, `.csproj`, `.json` files in the project and referenced projects
- On file change:
  - **Hot Reload** attempts to apply the change live (no restart)
  - If Hot Reload not possible → **automatic rebuild + restart**
- `--no-hot-reload` disables Hot Reload (always restart)
- `--launch-profile https` selects a profile from launchSettings.json
- Does **not** exit on app crash — restarts automatically

Task type: **Background task** (runs indefinitely).

### dotnet run vs dotnet watch

| | `dotnet run` | `dotnet watch` |
|---|---|---|
| Build | Once | On every change |
| Hot Reload | No | Yes |
| Auto-Restart | No | Yes |
| Cross-Project Changes | Not detected | Detected → Rebuild |
| New Files/Classes | Not detected | Detected → Restart |
| Debugger | Via VS F5 | Not directly (Attach possible) |
| Production use | No (`dotnet MyApp.dll`) | No (development only) |

Note: `dotnet start` does not exist as a .NET CLI command.

### dotnet build

```
dotnet build MyApp.csproj -c Release
```

- Compiles the project/solution
- `-c Debug|Release` selects the configuration
- `--no-restore` skips NuGet restore
- `--verbosity` controls output verbosity (quiet/minimal/normal/detailed/diagnostic)
- Exit code 0 = success, != 0 = build error

Task type: **Normal task**. Often used as a `dependsOn` prerequisite or pre-build step.

### dotnet clean

```
dotnet clean MyApp.csproj -c Release
```

- Removes build output (bin/obj folders)
- `-c Debug|Release` selects the configuration to clean
- Often combined with build: `dotnet clean && dotnet build` (= Rebuild)
- VS equivalent: Rebuild Solution (Ctrl+Alt+F7) = Clean + Build

Task type: **Normal task**.

### dotnet restore

```
dotnet restore MyApp.sln
```

- Restores NuGet packages defined in the project/solution
- Usually runs implicitly as part of `dotnet build` and `dotnet run`
- `--no-cache` forces fresh download (ignores local NuGet cache)
- `--locked-mode` fails if packages don't match the lock file (CI scenario)
- Explicit restore is needed when using `--no-restore` on subsequent commands

Task type: **Normal task**. Typically a prerequisite step, often implicit.

### dotnet test

```
dotnet test MyApp.Tests.csproj --filter "Category=Unit"
```

- Runs unit tests (MSTest, xUnit, NUnit)
- `--filter` filters by test category or name
- `--no-build` skips the build
- `--logger` controls output format (trx, console, html)
- Exit code 0 = all tests passed, 1 = tests failed

Task type: **Normal task**. Often in the `test` group in tasks.json.

### dotnet publish

```
dotnet publish MyApp.csproj -c Release -o ./publish
```

- Compiles and publishes the app (including all dependencies)
- `-o` specifies the output directory
- `--self-contained` creates a standalone app (includes .NET Runtime)
- `-r win-x64` specifies the target platform

Task type: **Normal task**. Typically in a "Build Production" group.

### dotnet ef (Entity Framework Core)

```
dotnet ef migrations add InitialCreate --project MyApp.Data
dotnet ef database update --project MyApp.Data
```

Requires the `dotnet-ef` tool (installed via `dotnet tool install --global dotnet-ef` or as a local tool).

- `migrations add <Name>` — create a new migration
- `migrations list` — list all migrations
- `database update` — update database to latest migration
- `database drop` — drop the database
- `--project` specifies the project containing the DbContext
- `--startup-project` specifies the project with the configuration

Task type: **Normal task**. Typically started manually, as migrations should not run automatically.

### dotnet msbuild

```
dotnet msbuild -t:WatchTailwindCss MyApp.csproj
```

- Executes specific MSBuild targets
- `-t:TargetName` selects the target
- `-p:Configuration=Debug` sets properties
- Can run multiple targets: `-t:Clean;Build`

Task type: **Normal task** in most cases. Note: A custom MSBuild target *can* start a background process (e.g., `watch-tailwind.cmd` that launches a watcher via `start /min`). In that case, the `dotnet msbuild` command itself exits (normal task), but the spawned background process continues running independently.

### msbuild.exe (.NET Framework)

```
msbuild.exe -t:BeforeBuild MyApp.csproj
```

The .NET Framework equivalent of `dotnet msbuild`. Shipped with Visual Studio (not the .NET SDK).

- Same `-t:` and `-p:` syntax as `dotnet msbuild`
- Required for non-SDK-style .csproj projects that don't support the `dotnet` CLI
- Path can be resolved via `vswhere.exe`: `vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe"`
- Also available via VS Developer Command Prompt (where `msbuild` is in PATH)

Task type: **Normal task**. Same role as `dotnet msbuild` but for .NET Framework projects.

### dotnet tool run

```
dotnet tool run dotnet-ef -- migrations list
```

Runs a locally installed .NET tool (defined in `.config/dotnet-tools.json`).

- Alternative to global installation
- `--` separates tool arguments from dotnet arguments
- Local tools are defined and versioned per repository

Task type: **Normal task**. Relevant when tasks use local tools instead of global ones.

## Node.js

### npm run

```
npm run <script-name>
```

Runs a script entry from the `scripts` section in `package.json`.

- Adds `node_modules/.bin` to PATH → locally installed tools (like `tailwindcss`) are directly callable
- Runs `pre<script>` before and `post<script>` after the script (if defined)
- Exit code of the script is passed through
- `npm run` (without argument) lists all available scripts
- Ctrl+C terminates the process

Task type: Depends on the script — see [Background Task Detection Heuristics](reference-task-config-files.md#background-task-detection-heuristics).

### pnpm run

```
pnpm run <script-name>
pnpm <script-name>
```

pnpm equivalent of `npm run`. Shorthand `pnpm <script>` works without `run`.

- Same behavior as `npm run` for executing scripts
- Makes locally installed binaries callable (via pnpm's symlinked store, functionally equivalent to npm's `node_modules/.bin` PATH addition)
- `pnpm dlx <tool>` is the equivalent of `npx` (run without installing)
- Strict: only declared dependencies are accessible (phantom dependency prevention)

Task type: Same as `npm run` — depends on the script.

### yarn run

```
yarn run <script-name>
yarn <script-name>
```

Yarn equivalent of `npm run`. Shorthand `yarn <script>` works without `run`.

- Same behavior as `npm run` for executing scripts
- `yarn dlx <tool>` is the equivalent of `npx` (Yarn Berry)
- Yarn Classic (v1): similar to npm, uses `node_modules`
- Yarn Berry (v2+): may need `yarn exec` to find binaries in PnP mode (no `node_modules/.bin`)

Task type: Same as `npm run` — depends on the script.

### npx

```
npx tailwindcss -i ./input.css -o ./output.css --watch
```

Runs an npm package without installing it globally.

- Looks first in `node_modules/.bin`, then in the npm registry
- Commonly used in tasks.json as `command` (e.g., `npx tailwindcss`, `npx tsc`)
- Advantage over `npm run`: No script entry in package.json required
- pnpm equivalent: `pnpm dlx`, yarn equivalent: `yarn dlx`

Task type: Depends on the tool and flags used.

### tsc (TypeScript Compiler)

```
tsc --watch --project tsconfig.json
```

- Compiles TypeScript to JavaScript
- `--watch` watches files and recompiles on changes
- `--project` / `-p` specifies the tsconfig.json
- `--noEmit` type-checks only, generates no output
- Often run via `npx tsc` or as an npm script

Task type: With `--watch` a **background task**, without a **normal task**.

### tailwindcss CLI

```
tailwindcss -i ./Styles/tailwind-source.css -o ./wwwroot/tailwind.css --watch
```

- Generates CSS from Tailwind utility classes
- `-i` input file, `-o` output file
- `--watch` watches files and regenerates on changes
- `--minify` minifies the output (for production)
- Often called via `npm run watchcss` / `npm run buildcss` or directly via `npx tailwindcss`

Task type: With `--watch` a **background task**, with `--minify` (without watch) a **normal task**. Directly relevant for the Blazor + Tailwind workflow.

## Build Tools (Legacy)

### grunt

```
grunt <task-name>
grunt build
grunt watch
```

CLI for the Grunt task runner. Executes tasks defined in `Gruntfile.js`.

- `grunt` (without argument) runs the `default` task
- `grunt <task>` runs a specific named task
- `grunt --verbose` shows detailed output
- Requires global install (`npm install -g grunt-cli`) or via `npx grunt`
- Tasks are configured in `Gruntfile.js` (see [Gruntfile.js in reference-task-config-files.md](reference-task-config-files.md))

Task type: **Normal task** for most tasks. The `watch` task (via `grunt-contrib-watch`) is a **background task**.

Grunt is considered **legacy** — most projects have migrated to npm scripts or modern bundlers.

### gulp

```
gulp <task-name>
gulp build
gulp watch
```

CLI for the Gulp task runner. Executes tasks defined in `gulpfile.js`.

- `gulp` (without argument) runs the `default` task
- `gulp <task>` runs a specific named task
- `gulp --tasks` lists all available tasks
- Requires global install (`npm install -g gulp-cli`) or via `npx gulp`
- Tasks are defined as JavaScript functions in `gulpfile.js` (see [gulpfile.js in reference-task-config-files.md](reference-task-config-files.md))

Task type: **Normal task** for most tasks. Tasks using `watch()` are **background tasks**.

Gulp is considered **legacy** — superseded by npm scripts and modern bundlers, but still found in projects with complex asset pipelines.

## Docker

### docker compose

```
docker compose up -d
docker compose down
docker compose logs -f
```

Manages multi-container environments defined in `compose.yml` (see [compose.yml in reference-task-config-files.md](reference-task-config-files.md)).

- `up` starts all services in foreground (with output streaming)
- `up -d` starts in background (detached)
- `down` stops and removes containers
- `logs -f` follows logs in real-time
- `up db redis` starts specific services only
- Configuration via `compose.yml` / `docker-compose.yml`

Task type: `docker compose up` (without -d) is a **background task**. `docker compose up -d` is a **normal task** (exits immediately). Often used in task groups as a prerequisite (e.g., "start database" before "start app").
