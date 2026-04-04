namespace TaskRunnerExtended.Tests.Services;

using TaskRunnerExtended.Models;
using TaskRunnerExtended.Services;

[TestClass]
[TestCategory("Unit")]
public class GroupConfigServiceTests
{
    private string _tempDir = null!;
    private GroupConfigService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"tre-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new GroupConfigService();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void LoadGroups_NoFiles_ReturnsEmpty()
    {
        var groups = _service.LoadGroups(_tempDir);
        Assert.AreEqual(0, groups.Count);
    }

    [TestMethod]
    public void SaveGroup_CreatesLocalFile()
    {
        _service.SaveGroup(_tempDir, new TaskGroup { Name = "Dev" });

        var groups = _service.LoadGroups(_tempDir);
        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual("Dev", groups[0].Name);
        Assert.IsTrue(File.Exists(Path.Combine(_tempDir, "task-runner-extended-am.local.json")));
    }

    [TestMethod]
    public void SaveGroup_SharedFile()
    {
        _service.SaveGroup(_tempDir, new TaskGroup { Name = "CI" }, toShared: true);

        Assert.IsTrue(File.Exists(Path.Combine(_tempDir, "task-runner-extended-am.json")));
    }

    [TestMethod]
    public void LoadGroups_MergesLocalOverShared()
    {
        // Shared has group "Dev" with 1 task
        _service.SaveGroup(_tempDir, new TaskGroup
        {
            Name = "Dev",
            Tasks = [new TaskGroupEntry { Source = "tasks.json", Task = "build" }],
        }, toShared: true);

        // Local has group "Dev" with 2 tasks (overrides shared)
        _service.SaveGroup(_tempDir, new TaskGroup
        {
            Name = "Dev",
            Tasks =
            [
                new TaskGroupEntry { Source = "tasks.json", Task = "watchcss" },
                new TaskGroupEntry { Source = "tasks.json", Task = "dotnet-watch" },
            ],
        });

        var groups = _service.LoadGroups(_tempDir);
        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(2, groups[0].Tasks.Count, "Local should override shared");
    }

    [TestMethod]
    public void AddTaskToGroup_CreatesGroupIfNotExists()
    {
        _service.AddTaskToGroup(_tempDir, "Dev", new TaskGroupEntry
        {
            Source = "package.json",
            Task = "watchcss",
        });

        var groups = _service.LoadGroups(_tempDir);
        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual("Dev", groups[0].Name);
        Assert.AreEqual(1, groups[0].Tasks.Count);
    }

    [TestMethod]
    public void AddTaskToGroup_NoDuplicates()
    {
        _service.AddTaskToGroup(_tempDir, "Dev", new TaskGroupEntry { Source = "tasks.json", Task = "build" });
        _service.AddTaskToGroup(_tempDir, "Dev", new TaskGroupEntry { Source = "tasks.json", Task = "build" });

        var groups = _service.LoadGroups(_tempDir);
        Assert.AreEqual(1, groups[0].Tasks.Count, "Should not add duplicate");
    }

    [TestMethod]
    public void DeleteGroup_RemovesFromTargetFile()
    {
        _service.SaveGroup(_tempDir, new TaskGroup { Name = "Dev" }, toShared: true);
        _service.SaveGroup(_tempDir, new TaskGroup { Name = "Dev" });

        // Delete from local only
        _service.DeleteGroup(_tempDir, "Dev", fromShared: false);
        Assert.AreEqual(0, _service.LoadLocalGroups(_tempDir).Count);
        Assert.AreEqual(1, _service.LoadSharedGroups(_tempDir).Count);

        // Delete from shared
        _service.DeleteGroup(_tempDir, "Dev", fromShared: true);
        Assert.AreEqual(0, _service.LoadSharedGroups(_tempDir).Count);
    }

    [TestMethod]
    public void RemoveTaskFromGroup_RemovesCorrectTask()
    {
        _service.AddTaskToGroup(_tempDir, "Dev", new TaskGroupEntry { Source = "tasks.json", Task = "build" });
        _service.AddTaskToGroup(_tempDir, "Dev", new TaskGroupEntry { Source = "tasks.json", Task = "watch" });

        _service.RemoveTaskFromGroup(_tempDir, "Dev", "tasks.json", "build");

        var groups = _service.LoadGroups(_tempDir);
        Assert.AreEqual(1, groups[0].Tasks.Count);
        Assert.AreEqual("watch", groups[0].Tasks[0].Task);
    }

    [TestMethod]
    public void GetGroupNames_ReturnsAllNames()
    {
        _service.SaveGroup(_tempDir, new TaskGroup { Name = "Dev" });
        _service.SaveGroup(_tempDir, new TaskGroup { Name = "CI" });

        var names = _service.GetGroupNames(_tempDir);
        Assert.AreEqual(2, names.Count);
        Assert.IsTrue(names.Contains("Dev"));
        Assert.IsTrue(names.Contains("CI"));
    }
}
