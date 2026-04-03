---
status: Stable
updated: 2026-04-03 20:30h
references:
  - .claude/feature.md — Main feature specification
  - .claude/reference-cli.md — CLI reference (dotnet, npm, docker, etc.)
---

# Reference: Task Configuration Files

Documents the file formats that serve as task sources.

## Overview: Visual Studio Task Runner Extended

The [visualstudio-task-runner-extended](https://github.com/ardimedia-com/visualstudio-task-runner-extended) extension discovers all standard task configuration files described in this document and presents them in a unified tree view inside Visual Studio. For the full feature set, schema, and UI details, visit the GitHub project.

**How developers use it:** The extension automatically scans for task configuration files as described in this document. Developers then group, reorder, and start these tasks directly from Visual Studio — individually or as parallel/sequential groups with one click. The extension stores group definitions in its own configuration files: `task-runner-extended-am.json` (team-shared, committed to git) and `task-runner-extended-am.local.json` (per-user, gitignored). The `am` prefix (ardimedia) prevents naming collisions with other extensions.

## Visual Studio

### ./*.csproj (MSBuild Project File)

C# project files can contain custom MSBuild targets with `<Exec>` commands. Both .NET (Core/.NET 5+) and .NET Framework use the .csproj format, but with different XML styles: SDK-style (compact, `<Project Sdk="...">`) for modern .NET, and non-SDK-style (verbose, `<Project ToolsVersion="...">`) for .NET Framework. Custom targets with `<Exec>` work identically in both — see [SDK-Style vs Non-SDK-Style](#sdk-style-vs-non-sdk-style-csproj) below for details.

**How developers use this file:** The .csproj is primarily the project definition (references, target framework, etc.), but developers add custom `<Target>` elements to hook shell commands into the build pipeline. Common use cases: running Tailwind CSS compilation before build, generating code, copying files post-build, or running custom tools. These targets run automatically during `dotnet build` based on their conditions. Individual targets can also be run manually via `dotnet msbuild -t:TargetName`.

#### Location

- `<project>/<ProjectName>.csproj`
- One per C# project

#### Relevant Section

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <!-- Custom target: runs Tailwind watcher during Debug builds -->
  <Target Name="WatchTailwindCss" BeforeTargets="Build"
          Condition="'$(Configuration)' == 'Debug' AND Exists('node_modules')">
    <Exec Command="call watch-tailwind.cmd"
          WorkingDirectory="$(ProjectDir)"
          IgnoreExitCode="true" />
  </Target>

  <!-- Custom target: builds Tailwind CSS for Release -->
  <Target Name="BuildTailwindCss" BeforeTargets="Build"
          Condition="'$(Configuration)' != 'Debug' AND Exists('node_modules')">
    <Exec Command="npm run buildcss"
          WorkingDirectory="$(ProjectDir)"
          ConsoleToMSBuild="true" />
  </Target>

</Project>
```

#### Custom Target Anatomy

`<Target>` elements with `<Exec>` commands are the relevant parts for task execution:

| Element/Attribute | Description |
|---|---|
| `<Target Name="...">` | Target name (used to run it: `dotnet msbuild -t:Name`) |
| `<Exec Command="...">` | The shell command to execute |
| `BeforeTargets="Build"` | Runs automatically before the Build target |
| `AfterTargets="Build"` | Runs automatically after the Build target |
| `Condition="..."` | MSBuild condition — evaluated at build time, not at definition time |
| `WorkingDirectory="..."` | Working directory for the Exec command |
| `IgnoreExitCode="true"` | Continue build even if the command fails |
| `ConsoleToMSBuild="true"` | Capture output for MSBuild logging |

#### Running Individual Targets

```
dotnet msbuild -t:WatchTailwindCss MyApp.csproj
msbuild.exe -t:WatchTailwindCss MyApp.csproj   (for .NET Framework)
```

Note: MSBuild conditions are evaluated at build time. When running a target directly via `-t:`, the condition may or may not apply depending on the current environment (e.g., `$(Configuration)` defaults to `Debug` unless overridden with `-p:Configuration=Release`).

#### Scope of Custom Targets

Custom targets with `<Exec>` are typically defined:
- **In the .csproj itself** — most common for project-specific tasks
- **In `Directory.Build.targets`** — shared across all projects in a directory tree
- **In imported `.targets` files** — from NuGet packages or shared infrastructure

Note: Only targets directly in the `.csproj` are easily discoverable by reading the file. Targets from imports require MSBuild evaluation to resolve.

#### SDK-Style vs Non-SDK-Style .csproj

There are two fundamentally different .csproj formats in the .NET ecosystem:

**SDK-style** (.NET Core / .NET 5+):
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <Target Name="MyTarget" BeforeTargets="Build">
    <Exec Command="npm run buildcss" />
  </Target>
</Project>
```

**Non-SDK-style** (.NET Framework):
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" DefaultTargets="Build"
         xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\..." />
  <PropertyGroup>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <!-- hundreds of lines: every file listed explicitly -->
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
    <Compile Include="App.config" />
    <!-- ... -->
  </ItemGroup>
  <Target Name="BeforeBuild">
    <Exec Command="npm run buildcss" />
  </Target>
</Project>
```

| Aspect | SDK-style (.NET Core/5+) | Non-SDK-style (.NET Framework) |
|---|---|---|
| Identifier | `<Project Sdk="Microsoft.NET.Sdk">` | `<Project ToolsVersion="...">` or no `Sdk` attribute |
| File size | Compact (10-50 lines typical) | Verbose (100-500+ lines, every file listed) |
| Target framework | `<TargetFramework>net10.0</TargetFramework>` | `<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>` |
| Custom targets | `BeforeTargets="Build"` / `AfterTargets="Build"` | Override `BeforeBuild` / `AfterBuild` target names |
| Build command | `dotnet build` / `dotnet msbuild` | `msbuild.exe` (from VS installation) |
| Run target | `dotnet msbuild -t:TargetName` | `msbuild.exe -t:TargetName` |
| Exec in targets | Same `<Exec Command="...">` syntax | Same `<Exec Command="...">` syntax |
| Imports | Implicit (SDK provides defaults) | Explicit (`<Import Project="...">`) |
| File listing | Implicit (glob: all .cs files included) | Explicit (`<Compile Include="File.cs" />`) |

**Key differences for custom targets:**
- **Target naming convention**: .NET Framework traditionally uses `BeforeBuild`/`AfterBuild` as target *names* (overriding built-in targets), while SDK-style uses `BeforeTargets="Build"`/`AfterTargets="Build"` as *attributes* on custom-named targets. Both patterns achieve the same result.
- **Build tool**: .NET Framework uses `msbuild.exe` (shipped with Visual Studio), while SDK-style uses `dotnet msbuild` (shipped with the .NET SDK). The `<Exec Command="...">` syntax is identical in both.
- **Detection**: SDK-style projects have `Sdk="..."` on the `<Project>` element. Non-SDK-style projects have `ToolsVersion="..."` or no `Sdk` attribute.

### ./Properties/launchSettings.json

Defines launch profiles for .NET (Core/.NET 5+) projects. Used by Visual Studio, VS Code, and `dotnet run`. This file **does not exist** in .NET Framework projects — those use Visual Studio's Project Properties dialog and store settings in binary `.suo` / XML `.csproj.user` files instead. See [.NET Framework Projects](#net-framework-projects-non-sdk-style) below for details.

**How developers use this file:** Developers configure how their app starts — which URLs to listen on, which environment variables to set, and whether to launch a browser. The first profile is the default when pressing F5 in Visual Studio. Teams typically have an "https" profile for debugging and a "dotnet watch" profile for auto-restart development. The file is committed to git (unlike most things in `Properties/`) so the team shares launch configurations. Developers switch between profiles via the toolbar dropdown in VS.

#### Location

- `<project>/Properties/launchSettings.json`
- One per .NET project that can be launched

#### Schema

```json
{
  "profiles": {
    "dotnet watch": {
      "commandName": "Executable",
      "executablePath": "dotnet",
      "commandLineArgs": "watch --project MyApp.csproj --launch-profile https",
      "workingDirectory": "$(ProjectDir)",
      "launchBrowser": true,
      "launchUrl": "https://localhost:5001",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:5001;http://localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "Docker": {
      "commandName": "Docker",
      "launchBrowser": true,
      "launchUrl": "{Scheme}://{ServiceHost}:{ServicePort}"
    }
  }
}
```

#### Profile Properties

| Property | Type | Description |
|---|---|---|
| `commandName` | string | `"Project"`, `"Executable"`, `"Docker"`, `"IISExpress"` |
| `executablePath` | string | Path to executable (for `"Executable"` command) |
| `commandLineArgs` | string | Command-line arguments |
| `workingDirectory` | string | Working directory |
| `launchBrowser` | boolean | Open browser on launch |
| `launchUrl` | string | URL to open in browser |
| `applicationUrl` | string | URLs the app listens on (semicolon-separated) |
| `environmentVariables` | object | Environment variables (`"KEY": "value"`) |
| `dotnetRunMessages` | boolean | Show dotnet run messages |

#### Profile Ordering

The **first** profile in the JSON is the default profile in Visual Studio. Developers change the default by reordering the profiles in the file.

#### Command Types

| commandName | What it does | VS Shortcut |
|---|---|---|
| `"Project"` | `dotnet run` with the project | F5 / Ctrl+F5 |
| `"Executable"` | Runs a custom executable | F5 / Ctrl+F5 |
| `"Docker"` | Starts via Docker | F5 / Ctrl+F5 |
| `"IISExpress"` | Starts via IIS Express (legacy) | F5 / Ctrl+F5 |

#### When launchSettings.json Does Not Exist

Not all .NET projects have a launchSettings.json:

- **Class libraries** (.csproj with `OutputType: Library`) — no launch profiles, since they can't be started directly
- **Console apps** — may or may not have one. Without it, `dotnet run` uses defaults (no browser, no custom env vars)
- **Test projects** — typically no launch profiles. Tests are run via `dotnet test`, not `dotnet run`
- **New projects** — the file is created by `dotnet new` templates for web projects, but not for all project types

When no launchSettings.json exists, there are simply no launch profiles for that project. This is normal — not every project needs one.

#### .NET Framework Projects (non-SDK style)

.NET Framework projects (`.csproj` without `Sdk="Microsoft.NET.Sdk"`) use a fundamentally different launch configuration:

| Aspect | .NET (Core) / .NET 5+ | .NET Framework |
|---|---|---|
| Launch config | `Properties/launchSettings.json` | Project properties in `.csproj.user` + `.suo` |
| Format | JSON (human-readable, committed to git) | Binary `.suo` (per-user) + XML `.csproj.user` |
| CLI launch | `dotnet run` / `dotnet watch` | Not supported (no `dotnet` CLI) |
| Web server | Kestrel (self-hosted) | IIS Express or full IIS |
| Start action | `commandName` in profile | `StartAction` in project properties |
| Env vars | `environmentVariables` in profile | System environment or web.config transforms |
| Debug config | `Properties/launchSettings.json` | `Properties/<Project>.csproj.user` |

**How developers use it:** .NET Framework projects are configured via Visual Studio's Project Properties dialog (Debug tab). Settings are stored in the binary `.suo` file (per-user, not in git) or in `<ProjectName>.csproj.user` (XML, typically gitignored).

**Note:** `.suo` files are binary and not human-readable. `.csproj.user` files are XML but typically gitignored. Neither format is designed for cross-IDE or cross-tool consumption — they are Visual Studio internal files.

### ./.vs/tasks.vs.json (Visual Studio Native Format)

Visual Studio's own task format, designed for **unrecognized codebases** that Visual Studio doesn't know how to build natively.

**How developers use this file:** In **Open Folder** mode (File → Open → Folder), developers right-click a file or folder in Solution Explorer and select **"Configure Tasks"** to create or open this file. However, this menu item **only appears for file types that Visual Studio does not recognize** as part of a known project system. For example, it appears on `makefile`, custom scripts, or unknown file types — but **not** on `.cs`, `.csproj`, or other files that VS already knows how to handle.

This means tasks.vs.json is primarily relevant for:
- **CMake projects** without a .sln
- **Makefile-based builds** (C/C++, custom toolchains)
- **Custom build systems** that VS doesn't natively support
- **Node.js / Python folders** opened without a .sln

For .NET projects (which VS recognizes), tasks.vs.json is rarely used. Developers use the **Task Runner Explorer** instead (see below).

The file lives in the `.vs` folder which is gitignored by default, so it's a per-user file. Once tasks are defined, Visual Studio adds them as commands to the Solution Explorer right-click context menu for matching files (based on the `appliesTo` pattern).

#### Location

- `<workspace>/.vs/tasks.vs.json`
- Stored in the `.vs` folder (typically gitignored)

#### Schema

```json
{
  "version": "0.2.1",
  "tasks": [
    {
      "taskLabel": "Build with Make",
      "appliesTo": "makefile",
      "type": "launch",
      "contextType": "build",
      "command": "nmake",
      "args": [ "build" ]
    },
    {
      "taskLabel": "Run Linter",
      "appliesTo": "*.py",
      "type": "command",
      "command": "pylint",
      "args": [ "${file}" ]
    }
  ]
}
```

#### Task Properties

| Property | Type | Description |
|---|---|---|
| `taskLabel` | string | Display name (note: `taskLabel`, not `label`) |
| `appliesTo` | string | File pattern this task applies to (`"/"` = root, `"*.cs"` = C# files) |
| `type` | string | `"command"`, `"msbuild"`, `"launch"` |
| `command` | string | Command to execute (for type `"command"`) |
| `args` | string[] | Command-line arguments |
| `workingDirectory` | string | Working directory |
| `envVars` | object | Environment variables |
| `msbuildConfiguration` | string | MSBuild configuration (for type `"msbuild"`) |
| `msbuildPlatform` | string | MSBuild platform |
| `contextType` | string | Context: `"build"`, `"clean"`, `"rebuild"` |
| `output` | string | Output file path |
| `inheritEnvironments` | string[] | Environment presets to inherit |

#### Key Differences from tasks.json

| Aspect | tasks.json (VS Code) | tasks.vs.json (VS) |
|---|---|---|
| Label field | `label` | `taskLabel` |
| Location | `.vscode/tasks.json` | `.vs/tasks.vs.json` |
| Working dir field | `options.cwd` | `workingDirectory` |
| Env vars field | `options.env` | `envVars` |
| Compound tasks | `dependsOn` + `dependsOrder` | Not supported |
| Background tasks | `isBackground` | Not supported |
| File targeting | N/A | `appliesTo` (context menu on files) |
| Problem matchers | Full regex patterns | Built-in matchers only |
| OS overrides | `windows`/`linux`/`osx` | N/A (Windows only) |

### ./.vs/launch.vs.json (Visual Studio Debug Configuration)

The companion file to tasks.vs.json, used to configure **debug settings** for codebases in Open Folder mode.

**How developers use this file:** In Open Folder mode, developers right-click an executable file in Solution Explorer and select **"Add Debug Configuration"**. Visual Studio prompts for a debugger type and creates the launch.vs.json file in the `.vs` folder. Like tasks.vs.json, this is only relevant for codebases that VS doesn't recognize natively — .NET projects use launchSettings.json instead.

#### Location

- `<workspace>/.vs/launch.vs.json`
- Stored in the `.vs` folder (typically gitignored)

#### Schema

```json
{
  "version": "0.2.1",
  "defaults": {},
  "configurations": [
    {
      "type": "default",
      "project": "bin\\hello.exe",
      "projectTarget": "",
      "name": "hello.exe"
    },
    {
      "type": "default",
      "project": "bin\\hello.exe",
      "name": "hello.exe with args",
      "args": [ "arg1", "arg2" ]
    }
  ]
}
```

#### Configuration Properties

| Property | Type | Description |
|---|---|---|
| `type` | string | Debugger type (`"default"`, `"native"`, `"node"`, etc.) |
| `project` | string | Relative path to the executable |
| `projectTarget` | string | Target within the project |
| `name` | string | Display name in the debug target dropdown |
| `args` | string[] | Command-line arguments passed to the debugger |
| `cwd` | string | Working directory for debugging |
| `env` | object | Environment variables |

#### Relationship to launchSettings.json

| Aspect | launch.vs.json | launchSettings.json |
|---|---|---|
| Purpose | Debug config for unrecognized codebases | Launch profiles for .NET projects |
| When used | Open Folder mode (no .sln) | Solution mode and `dotnet run` |
| Created via | Right-click → "Add Debug Configuration" | `dotnet new` templates or manual |
| Storage | `.vs/launch.vs.json` (gitignored) | `Properties/launchSettings.json` (committed to git) |
| Used by | Visual Studio only | Visual Studio, VS Code, dotnet CLI |
| Multiple configs | `configurations` array | Multiple named profiles |

For .NET projects, launchSettings.json is always the correct choice. launch.vs.json is only needed for debugging executables in codebases without a .sln/.csproj.

### Task Runner Explorer

A built-in Visual Studio feature that auto-discovers tasks from build tools. Available via right-click in Solution Explorer or View → Other Windows → Task Runner Explorer.

**How developers use it:** The Task Runner Explorer automatically detects tasks from supported build tools (Grunt, Gulp, and via extensions also npm scripts). Developers can run tasks directly and bind them to build lifecycle events. It works in **both** Solution mode and Open Folder mode, making it the most universally available task system in Visual Studio.

#### Access

- Right-click on a project or solution in Solution Explorer → **"Task Runner Explorer"**
- Menu: View → Other Windows → **Task Runner Explorer**

#### Supported Task Sources

Out of the box, Visual Studio supports:
- **Grunt** — discovers tasks from `Gruntfile.js`
- **Gulp** — discovers tasks from `gulpfile.js`

With extensions:
- **NPM Task Runner** — discovers scripts from `package.json`
- Other community extensions for additional build tools

#### Build Bindings

Tasks can be bound to Visual Studio build lifecycle events:

| Binding | When it runs |
|---|---|
| Before Build | Before the solution/project builds |
| After Build | After a successful build |
| Clean | When the solution/project is cleaned |
| Project Open | When the solution/project is opened |

Bindings are stored in the `.vs/` folder (per-user) or as comments at the top of the task file (e.g., `/// <binding BeforeBuild='build' />` in Gruntfile.js).

#### Comparison with tasks.vs.json

| Aspect | Task Runner Explorer | tasks.vs.json |
|---|---|---|
| Purpose | Discover tasks from build tools | Custom tasks for unrecognized codebases |
| When available | Always (Solution and Open Folder mode) | Open Folder mode, on unrecognized file types |
| Sources | Auto-discovered from Gruntfile, Gulpfile, package.json | Manually configured via "Configure Tasks" |
| Build bindings | Before Build, After Build, Clean, Project Open | Via `contextType` property |
| Extensibility | Third-party extensions add runners | Fixed format |

For most .NET + Node.js projects, the Task Runner Explorer is the relevant system — it auto-discovers npm scripts from package.json (with NPM Task Runner extension) and allows binding them to build events.

## VS Code

### ./.vscode/tasks.json (VS Code Task Format)

The primary, IDE-agnostic task configuration format. Defined by VS Code but usable in any editor.

**How developers use this file:** Developers manually create or edit this file to define repeatable build, test, and watch commands for their project. It serves as the single source of truth for "how to run things" in a repo — new team members open the project and immediately see all available tasks. Compound tasks (via `dependsOn`) allow starting an entire dev environment with one command (e.g., "dev" starts both CSS watcher and dotnet watch). The file is committed to git so the whole team shares the same task definitions.

#### Location

- `<workspace>/.vscode/tasks.json`
- Can exist at solution level, project level, or parent directories

#### Schema

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build",
      "type": "shell",
      "command": "dotnet",
      "args": ["build", "${workspaceFolder}/MyApp.csproj"],
      "group": {
        "kind": "build",
        "isDefault": true
      },
      "problemMatcher": "$msCompile"
    }
  ]
}
```

#### Task Properties

| Property | Type | Description |
|---|---|---|
| `label` | string | **Required.** Display name of the task |
| `type` | `"shell"` \| `"process"` | Shell executes via shell (cmd.exe), process runs directly |
| `command` | string | **Required.** The command to execute |
| `args` | string[] | Command-line arguments |
| `options.cwd` | string | Working directory (default: directory of tasks.json) |
| `options.env` | object | Additional environment variables (`"KEY": "value"`) |
| `options.shell` | object | Override default shell (`executable`, `args`) |
| `isBackground` | boolean | `true` for long-running watcher tasks |
| `dependsOn` | string \| string[] | Other task labels that must run first |
| `dependsOrder` | `"parallel"` \| `"sequence"` | How dependsOn tasks are started (default: parallel) |
| `group` | string \| object | Task category: `"build"`, `"test"`, or `{ "kind": "build", "isDefault": true }` |
| `presentation` | object | How the task output is shown (see below) |
| `problemMatcher` | string \| object \| array | Pattern to extract errors from output |
| `windows` / `linux` / `osx` | object | OS-specific overrides for any property |
| `runOptions` | object | `reevaluateOnRerun`, `instanceLimit` |

#### Presentation Object

| Property | Values | Description |
|---|---|---|
| `reveal` | `"always"` \| `"silent"` \| `"never"` | When to show the output panel |
| `panel` | `"shared"` \| `"dedicated"` \| `"new"` | Output panel reuse behavior |
| `group` | string | Group tasks in the same output panel |
| `close` | boolean | Close panel when task exits |
| `echo` | boolean | Echo the command in the output |
| `showReuseMessage` | boolean | Show "Terminal will be reused" message |
| `clear` | boolean | Clear output before running |

#### Variable Substitution

| Variable | Resolves to |
|---|---|
| `${workspaceFolder}` | Root workspace folder path |
| `${workspaceFolderBasename}` | Name of the workspace folder |
| `${file}` | Currently opened file (full path) |
| `${fileBasename}` | Currently opened file name |
| `${fileDirname}` | Directory of the currently opened file |
| `${fileExtname}` | Extension of the currently opened file |
| `${cwd}` | Current working directory on startup |
| `${env:VARIABLE}` | Environment variable |
| `${input:id}` | Prompted input variable (requires interactive UI at runtime) |

#### Compound Tasks Example

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "watchcss",
      "type": "shell",
      "command": "npm run watchcss",
      "isBackground": true
    },
    {
      "label": "dotnet-watch",
      "type": "shell",
      "command": "dotnet watch --project MyApp.csproj",
      "isBackground": true
    },
    {
      "label": "dev",
      "dependsOn": ["watchcss", "dotnet-watch"],
      "dependsOrder": "parallel",
      "group": {
        "kind": "build",
        "isDefault": true
      }
    }
  ]
}
```

## Node.js

### ./package.json

Node.js project manifest with a `scripts` section that defines runnable tasks. Used by all Node.js package managers (npm, pnpm, yarn).

**How developers use this file:** Developers define project-specific commands in the `scripts` section — build steps, watchers, linters, test runners, and dev servers. In Blazor projects, this is typically where `buildcss` and `watchcss` scripts live for Tailwind CSS compilation. The file is committed to git and serves as the project's "command registry" for anything Node.js related.

#### Location

- `<project>/package.json`
- Can exist at multiple levels in a monorepo

#### Relevant Section

```json
{
  "name": "my-app",
  "scripts": {
    "build": "tsc",
    "buildcss": "tailwindcss -i ./Styles/tailwind-source.css -o ./wwwroot/tailwind.css --minify",
    "watchcss": "tailwindcss -i ./Styles/tailwind-source.css -o ./wwwroot/tailwind.css --watch",
    "dev": "concurrently \"npm run watchcss\" \"dotnet watch\"",
    "test": "jest",
    "lint": "eslint .",
    "prestart": "npm run build",
    "start": "node dist/index.js"
  }
}
```

#### Script Conventions

| Pattern | Meaning | Task Type |
|---|---|---|
| `build`, `buildcss` | One-time build | Normal task |
| `watch`, `watchcss`, `dev` | File watcher | Background task |
| `test`, `lint` | Validation | Normal task |
| `start`, `serve` | Start server | Background task |
| `pre<script>` | Runs automatically before `<script>` | N/A (automatic) |
| `post<script>` | Runs automatically after `<script>` | N/A (automatic) |

#### Background Task Detection Heuristics

A script is likely a background task if:
- The script name contains `watch`, `dev`, `serve`, or `start`
- The command contains `--watch`, `--serve`, or `concurrently`
- The command uses tools known to run indefinitely (`nodemon`, `live-server`, etc.)

### Package Managers

All three package managers use the same `package.json` format and `scripts` section. They differ in lock file, CLI commands, and dependency resolution. The package manager is determined by which lock file is present. If multiple lock files exist, the conventional priority is: pnpm > yarn > npm.

#### npm

The default Node.js package manager, bundled with Node.js.

- **Lock file:** `package-lock.json`
- **Run command:** `npm run <script>`
- **Execute without install:** `npx <tool>`
- Supports `pre<script>` / `post<script>` lifecycle hooks automatically
- Most widely used; the scripts section in package.json was originally designed for npm

**How developers use it:** `npm run watchcss`, `npm run build`, etc. The `npx` command runs tools without global installation (e.g., `npx tailwindcss`).

#### pnpm

Fast, disk-space efficient alternative to npm. Uses a content-addressable store.

- **Lock file:** `pnpm-lock.yaml`
- **Run command:** `pnpm run <script>` (or just `pnpm <script>`)
- **Execute without install:** `pnpm dlx <tool>`
- Strict dependency resolution (packages can only access declared dependencies)
- Workspace support for monorepos via `pnpm-workspace.yaml`

**How developers use it:** Same scripts as npm, but `pnpm run` instead of `npm run`. Increasingly popular in monorepos due to workspace support and performance.

#### yarn

Alternative package manager by Meta (formerly Facebook). Two major versions with different architectures.

- **Lock file:** `yarn.lock`
- **Run command:** `yarn run <script>` (or just `yarn <script>`)
- **Execute without install:** `yarn dlx <tool>`
- Yarn Classic (v1): Similar to npm, uses `node_modules`
- Yarn Berry (v2+): Plug'n'Play (PnP), no `node_modules` by default — uses `.pnp.cjs` and `.yarn/cache`

**How developers use it:** Same scripts as npm, but `yarn` instead of `npm run`.

**Yarn Berry PnP caveat:** Yarn Berry (v2+) with Plug'n'Play does not create a `node_modules` folder. Tools are resolved via `.pnp.cjs` instead of `node_modules/.bin`. Shell commands may need `yarn exec` or `yarn run` to find binaries. A project using PnP can be identified by the presence of `.pnp.cjs` or `.pnp.js` in the project root.

## Build Tools (Task Runner Explorer Sources)

These files are auto-discovered by the Visual Studio Task Runner Explorer. They are primarily legacy build tools — most modern projects use npm scripts or tasks.json instead. Documented here because the Task Runner Explorer still supports them out of the box.

### ./Gruntfile.js

Configuration file for **Grunt**, a JavaScript-based task runner.

**How developers use this file:** Developers define tasks (compile, minify, lint, test) as JavaScript functions. Grunt was the dominant build tool in the JavaScript ecosystem from ~2012-2015, before being largely replaced by Gulp, then webpack/Vite, and finally npm scripts. It is still found in older projects.

#### Location

- `<project>/Gruntfile.js`
- One per project

#### Relevant Section

```javascript
module.exports = function(grunt) {
  grunt.initConfig({
    uglify: {
      build: {
        src: 'src/app.js',
        dest: 'dist/app.min.js'
      }
    },
    cssmin: {
      build: {
        src: 'src/styles.css',
        dest: 'dist/styles.min.css'
      }
    }
  });

  grunt.loadNpmTasks('grunt-contrib-uglify');
  grunt.loadNpmTasks('grunt-contrib-cssmin');

  grunt.registerTask('default', ['uglify', 'cssmin']);
  grunt.registerTask('build', ['uglify', 'cssmin']);
};
```

#### Key Concepts

| Concept | Description |
|---|---|
| `grunt.initConfig({})` | Configures task plugins with options and file targets |
| `grunt.loadNpmTasks()` | Loads a Grunt plugin from node_modules |
| `grunt.registerTask()` | Registers a named task (can combine multiple subtasks) |
| `default` task | Runs when `grunt` is called without arguments |

#### Task Runner Explorer Integration

- Visual Studio discovers all registered tasks automatically
- Tasks appear in Task Runner Explorer grouped by name
- Build bindings can be set via UI or via comments: `/// <binding BeforeBuild='build' />`

#### Current Status

Grunt is considered **legacy**. Most projects have migrated to npm scripts or modern bundlers (webpack, Vite, esbuild). New projects should not use Grunt. However, many existing enterprise projects still have a Gruntfile.js.

### ./gulpfile.js

Configuration file for **Gulp**, a streaming JavaScript task runner.

**How developers use this file:** Developers define tasks as JavaScript functions that process file streams (read → transform → write). Gulp was popular from ~2014-2018 as a faster, code-over-configuration alternative to Grunt. Like Grunt, it has been largely replaced by npm scripts and modern bundlers, but is still found in existing projects.

#### Location

- `<project>/gulpfile.js`
- One per project
- Can also be `gulpfile.ts` (TypeScript) or `gulpfile.babel.js` (ES modules)

#### Relevant Section

```javascript
const { src, dest, series, parallel, watch } = require('gulp');
const sass = require('gulp-sass')(require('sass'));
const uglify = require('gulp-uglify');

function styles() {
  return src('src/styles/**/*.scss')
    .pipe(sass().on('error', sass.logError))
    .pipe(dest('dist/css'));
}

function scripts() {
  return src('src/js/**/*.js')
    .pipe(uglify())
    .pipe(dest('dist/js'));
}

function watchFiles() {
  watch('src/styles/**/*.scss', styles);
  watch('src/js/**/*.js', scripts);
}

exports.default = series(styles, scripts);
exports.build = series(styles, scripts);
exports.watch = watchFiles;
exports.styles = styles;
exports.scripts = scripts;
```

#### Key Concepts

| Concept | Description |
|---|---|
| `src()` / `dest()` | Read files from / write files to the file system |
| `.pipe()` | Chain transformations (streaming) |
| `series()` | Run tasks sequentially |
| `parallel()` | Run tasks in parallel |
| `watch()` | Watch files for changes and run tasks |
| `exports.taskName` | Expose a function as a named task (Gulp 4+) |

#### Task Runner Explorer Integration

- Visual Studio discovers all exported tasks automatically
- Tasks appear in Task Runner Explorer grouped by name
- Build bindings work the same as with Grunt

#### Gulp vs Grunt

| Aspect | Grunt | Gulp |
|---|---|---|
| Approach | Configuration-based (JSON-like objects) | Code-based (JavaScript functions) |
| File handling | Reads/writes entire files per task | Streams (pipes, in-memory transforms) |
| Performance | Slower (disk I/O per step) | Faster (streaming, fewer disk writes) |
| Concurrency | Sequential by default | `parallel()` built-in |
| Config file | `Gruntfile.js` | `gulpfile.js` |

#### Current Status

Gulp is considered **legacy** for new projects. Like Grunt, it has been superseded by npm scripts and modern bundlers. However, Gulp's streaming API made it more flexible than Grunt, and some projects still actively use it — especially those with complex asset pipelines.

## Docker

### ./compose.yml (Docker Compose)

Defines multi-container environments for local development. Not a task configuration file per se, but `docker compose` commands are frequently run as tasks.

**How developers use this file:** Developers define the services their application depends on (databases, caches, message queues, mock APIs) and start them all with `docker compose up`. This is often the first step in a development workflow — start infrastructure, then start the app. The file is committed to git so the whole team uses the same local environment.

#### Location

- `<project>/compose.yml` (preferred, Docker Compose v2+)
- `<project>/docker-compose.yml` (legacy name, still supported)
- `<project>/compose.override.yml` (local overrides, often gitignored)

#### Relevant Section

```yaml
services:
  db:
    image: postgres:17
    ports:
      - "5432:5432"
    environment:
      POSTGRES_PASSWORD: devpassword
      POSTGRES_DB: myapp
    volumes:
      - pgdata:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  mailpit:
    image: axllent/mailpit
    ports:
      - "1025:1025"
      - "8025:8025"

volumes:
  pgdata:
```

#### Key Concepts

| Concept | Description |
|---|---|
| `services` | Named containers to run (each has an image, ports, env vars, volumes) |
| `image` | Docker image to pull and run |
| `ports` | Port mapping (host:container) |
| `environment` | Environment variables for the container |
| `volumes` | Persistent data storage |
| `depends_on` | Service startup order |
| `profiles` | Group services that should only start on demand |

#### Common Commands as Tasks

| Command | Task Type | Description |
|---|---|---|
| `docker compose up` | Background task | Start all services in foreground (with output) |
| `docker compose up -d` | Normal task | Start all services detached (exits immediately) |
| `docker compose down` | Normal task | Stop and remove all containers |
| `docker compose logs -f` | Background task | Follow logs in real-time |
| `docker compose up db redis` | Background task | Start specific services only |

#### Override Files

Docker Compose supports layered configuration:
- `compose.yml` — base configuration (committed to git)
- `compose.override.yml` — automatically merged on top (often gitignored for local tweaks)
- `compose.dev.yml` — explicit override: `docker compose -f compose.yml -f compose.dev.yml up`

This allows developers to customize ports, volumes, or additional services without modifying the shared base file.
