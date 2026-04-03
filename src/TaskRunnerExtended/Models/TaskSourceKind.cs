namespace TaskRunnerExtended.Models;

/// <summary>
/// Identifies which type of configuration file a task was discovered from.
/// </summary>
public enum TaskSourceKind
{
    TasksJson,
    TasksVsJson,
    PackageJson,
    Csproj,
    LaunchSettings,
    Gruntfile,
    Gulpfile,
    ComposeYml,
}
