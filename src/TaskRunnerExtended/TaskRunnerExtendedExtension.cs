namespace TaskRunnerExtended;

using Microsoft.VisualStudio.Extensibility;

/// <summary>
/// Entry point for the Task Runner Extended extension.
/// </summary>
[VisualStudioContribution]
public class TaskRunnerExtendedExtension : Extension
{
    /// <inheritdoc />
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new(
            id: "TaskRunnerExtended.C3D4E5F6-A7B8-9012-CDEF-345678901234",
            version: ExtensionAssemblyVersion,
            publisherName: "Ardimedia",
            displayName: "Task Runner Extended",
            description: "Discovers tasks from 8 sources, displays them in a unified tree view, and allows grouping and parallel execution in Visual Studio.")
        {
            Icon = ImageMoniker.Custom("Images/TaskRunnerExtended.128.128.png"),
            DotnetTargetVersions = [".net10.0"],
        },
    };
}
