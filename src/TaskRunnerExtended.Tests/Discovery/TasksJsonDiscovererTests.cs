namespace TaskRunnerExtended.Tests.Discovery;

using TaskRunnerExtended.Models;
using TaskRunnerExtended.Services.Discovery;

[TestClass]
[TestCategory("Unit")]
public class TasksJsonDiscovererTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"tre-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task DiscoverAsync_NoFile_ReturnsEmpty()
    {
        var discoverer = new TasksJsonDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task DiscoverAsync_ValidTasksJson_ReturnsTasks()
    {
        var vscodeDir = Path.Combine(_tempDir, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        File.WriteAllText(Path.Combine(vscodeDir, "tasks.json"), """
        {
            "version": "2.0.0",
            "tasks": [
                {
                    "label": "build",
                    "command": "dotnet",
                    "args": ["build"],
                    "type": "process"
                },
                {
                    "label": "watchcss",
                    "command": "npm run watchcss",
                    "isBackground": true
                }
            ]
        }
        """);

        var discoverer = new TasksJsonDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual(2, result.Count);

        var build = result.First(t => t.Label == "build");
        Assert.AreEqual("dotnet", build.Command);
        Assert.IsFalse(build.IsShell);
        Assert.AreEqual(TaskType.Normal, build.TaskType);
        CollectionAssert.AreEqual(new[] { "build" }, build.Args);

        var watchcss = result.First(t => t.Label == "watchcss");
        Assert.AreEqual("npm run watchcss", watchcss.Command);
        Assert.IsTrue(watchcss.IsShell);
        Assert.AreEqual(TaskType.Background, watchcss.TaskType);
    }

    [TestMethod]
    public async Task DiscoverAsync_CompoundTaskSkipped_InPhase1()
    {
        var vscodeDir = Path.Combine(_tempDir, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        File.WriteAllText(Path.Combine(vscodeDir, "tasks.json"), """
        {
            "version": "2.0.0",
            "tasks": [
                {
                    "label": "dev",
                    "dependsOn": ["watchcss", "dotnet-watch"],
                    "dependsOrder": "parallel"
                }
            ]
        }
        """);

        var discoverer = new TasksJsonDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual(0, result.Count, "Compound tasks without command should be skipped in Phase 1");
    }

    [TestMethod]
    public async Task DiscoverAsync_MalformedJson_ReturnsError()
    {
        var vscodeDir = Path.Combine(_tempDir, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        File.WriteAllText(Path.Combine(vscodeDir, "tasks.json"), "{ invalid json }}}");

        var discoverer = new TasksJsonDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result[0].IsError);
        Assert.IsTrue(result[0].Label.StartsWith("Parse Error:"));
    }

    [TestMethod]
    public async Task DiscoverAsync_WorkingDirectory_DefaultsToScanDir()
    {
        var vscodeDir = Path.Combine(_tempDir, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        File.WriteAllText(Path.Combine(vscodeDir, "tasks.json"), """
        {
            "version": "2.0.0",
            "tasks": [{ "label": "test", "command": "echo hello" }]
        }
        """);

        var discoverer = new TasksJsonDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual(_tempDir, result[0].WorkingDirectory);
    }

    [TestMethod]
    public async Task DiscoverAsync_GroupMetadata_Extracted()
    {
        var vscodeDir = Path.Combine(_tempDir, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        File.WriteAllText(Path.Combine(vscodeDir, "tasks.json"), """
        {
            "version": "2.0.0",
            "tasks": [
                {
                    "label": "build",
                    "command": "dotnet build",
                    "group": { "kind": "build", "isDefault": true }
                }
            ]
        }
        """);

        var discoverer = new TasksJsonDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual("build", result[0].Metadata);
    }
}
