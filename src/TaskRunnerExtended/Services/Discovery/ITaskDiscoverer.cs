namespace TaskRunnerExtended.Services.Discovery;

using TaskRunnerExtended.Models;

/// <summary>
/// Interface for discovering tasks from a specific source file type.
/// Each implementation handles one <see cref="TaskSourceKind"/>.
/// </summary>
public interface ITaskDiscoverer
{
    /// <summary>The kind of source this discoverer handles.</summary>
    TaskSourceKind SourceKind { get; }

    /// <summary>
    /// Scans a directory for source files and returns discovered tasks.
    /// </summary>
    /// <param name="directory">Directory to scan (project or solution directory).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All tasks discovered from matching files in the directory.</returns>
    Task<IReadOnlyList<TaskItem>> DiscoverAsync(string directory, CancellationToken cancellationToken);
}
