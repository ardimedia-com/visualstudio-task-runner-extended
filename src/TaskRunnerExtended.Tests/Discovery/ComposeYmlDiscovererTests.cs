namespace TaskRunnerExtended.Tests.Discovery;

using TaskRunnerExtended.Models;
using TaskRunnerExtended.Services.Discovery;

[TestClass]
[TestCategory("Unit")]
public class ComposeYmlDiscovererTests
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
        var discoverer = new ComposeYmlDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task DiscoverAsync_ValidComposeYml_ReturnsServices()
    {
        File.WriteAllText(Path.Combine(_tempDir, "compose.yml"), """
        services:
          db:
            image: postgres:17
          redis:
            image: redis:7-alpine
        """);

        var discoverer = new ComposeYmlDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        // 2 services + 1 "all services" task
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("docker: (all services)", result[0].Label);
        Assert.AreEqual("docker: db", result[1].Label);
        Assert.AreEqual("docker: redis", result[2].Label);
        Assert.IsTrue(result.All(t => t.TaskType == TaskType.Background));
    }

    [TestMethod]
    public async Task DiscoverAsync_SingleService_NoAllServicesTask()
    {
        File.WriteAllText(Path.Combine(_tempDir, "docker-compose.yml"), """
        services:
          db:
            image: postgres:17
        """);

        var discoverer = new ComposeYmlDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("docker: db", result[0].Label);
    }

    [TestMethod]
    public async Task DiscoverAsync_ImageAsMetadata()
    {
        File.WriteAllText(Path.Combine(_tempDir, "compose.yml"), """
        services:
          db:
            image: postgres:17
        """);

        var discoverer = new ComposeYmlDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual("postgres:17", result[0].Metadata);
    }
}
