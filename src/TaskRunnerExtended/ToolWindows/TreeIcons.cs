namespace TaskRunnerExtended.ToolWindows;

/// <summary>
/// VS KnownMonikers as strings for vs:Image Source binding in Remote UI.
/// Using string format "KnownMonikers.Name" as documented.
/// </summary>
public static class TreeIcons
{
    // Root nodes
    public const string ConfigFiles = "KnownMonikers.DocumentCollection";
    public const string RunGroups = "KnownMonikers.CheckBoxList";

    // Source file types
    public const string Json = "KnownMonikers.JSONScript";
    public const string Csproj = "KnownMonikers.CSProjectNode";
    public const string Docker = "KnownMonikers.CloudFoundry";
    public const string Npm = "KnownMonikers.PackageFolder";
    public const string Launch = "KnownMonikers.Run";
    public const string Grunt = "KnownMonikers.BuildSelection";
    public const string Gulp = "KnownMonikers.BuildSelection";
    public const string Folder = "KnownMonikers.FolderOpened";

    // Group
    public const string Group = "KnownMonikers.GroupByType";

    // Compound task
    public const string CompoundTask = "KnownMonikers.GroupByType";

    // Parse error
    public const string ParseError = "KnownMonikers.StatusWarning";

    // Task status
    public const string TaskIdle = "KnownMonikers.Run";
    public const string TaskRunning = "KnownMonikers.Sync";
    public const string TaskError = "KnownMonikers.StatusError";

    // Group status
    public const string GroupIdle = "KnownMonikers.Run";
    public const string GroupRunning = "KnownMonikers.Sync";

    /// <summary>
    /// Returns the icon string for a given source kind.
    /// </summary>
    public static string ForSourceKind(Models.TaskSourceKind kind) => kind switch
    {
        Models.TaskSourceKind.TasksJson => Json,
        Models.TaskSourceKind.TasksVsJson => Json,
        Models.TaskSourceKind.PackageJson => Npm,
        Models.TaskSourceKind.Csproj => Csproj,
        Models.TaskSourceKind.LaunchSettings => Launch,
        Models.TaskSourceKind.Gruntfile => Grunt,
        Models.TaskSourceKind.Gulpfile => Gulp,
        Models.TaskSourceKind.ComposeYml => Docker,
        _ => Folder,
    };
}
