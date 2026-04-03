namespace TaskRunnerExtended.Tests.Discovery;

using TaskRunnerExtended.Models;
using TaskRunnerExtended.Services.Discovery;

[TestClass]
[TestCategory("Unit")]
public class PackageJsonDiscovererTests
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
        var discoverer = new PackageJsonDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task DiscoverAsync_ValidPackageJson_ReturnsScripts()
    {
        File.WriteAllText(Path.Combine(_tempDir, "package.json"), """
        {
            "scripts": {
                "build": "tsc",
                "watchcss": "tailwindcss --watch",
                "test": "jest"
            }
        }
        """);

        var discoverer = new PackageJsonDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual(3, result.Count);

        var build = result.First(t => t.Label == "npm: build");
        Assert.AreEqual(TaskType.Normal, build.TaskType);

        var watchcss = result.First(t => t.Label == "npm: watchcss");
        Assert.AreEqual(TaskType.Background, watchcss.TaskType);
    }

    [TestMethod]
    public async Task DiscoverAsync_DetectsPnpm()
    {
        File.WriteAllText(Path.Combine(_tempDir, "package.json"), """{ "scripts": { "build": "tsc" } }""");
        File.WriteAllText(Path.Combine(_tempDir, "pnpm-lock.yaml"), "");

        var discoverer = new PackageJsonDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual("pnpm: build", result[0].Label);
        Assert.AreEqual("pnpm", result[0].Command);
    }

    [TestMethod]
    public async Task DiscoverAsync_DetectsYarn()
    {
        File.WriteAllText(Path.Combine(_tempDir, "package.json"), """{ "scripts": { "build": "tsc" } }""");
        File.WriteAllText(Path.Combine(_tempDir, "yarn.lock"), "");

        var discoverer = new PackageJsonDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual("yarn: build", result[0].Label);
        Assert.AreEqual("yarn", result[0].Command);
    }

    [TestMethod]
    public async Task DiscoverAsync_PnpmPriorityOverYarn()
    {
        File.WriteAllText(Path.Combine(_tempDir, "package.json"), """{ "scripts": { "build": "tsc" } }""");
        File.WriteAllText(Path.Combine(_tempDir, "pnpm-lock.yaml"), "");
        File.WriteAllText(Path.Combine(_tempDir, "yarn.lock"), "");

        var discoverer = new PackageJsonDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual("pnpm", result[0].Command, "pnpm should take priority over yarn");
    }

    [TestMethod]
    public async Task DiscoverAsync_SkipsPrePostScripts()
    {
        File.WriteAllText(Path.Combine(_tempDir, "package.json"), """
        {
            "scripts": {
                "prebuild": "clean",
                "build": "tsc",
                "postbuild": "copy"
            }
        }
        """);

        var discoverer = new PackageJsonDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("npm: build", result[0].Label);
    }

    [TestMethod]
    public async Task DiscoverAsync_BackgroundDetection()
    {
        File.WriteAllText(Path.Combine(_tempDir, "package.json"), """
        {
            "scripts": {
                "dev": "vite",
                "serve": "http-server",
                "lint": "eslint ."
            }
        }
        """);

        var discoverer = new PackageJsonDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual(TaskType.Background, result.First(t => t.Label == "npm: dev").TaskType);
        Assert.AreEqual(TaskType.Background, result.First(t => t.Label == "npm: serve").TaskType);
        Assert.AreEqual(TaskType.Normal, result.First(t => t.Label == "npm: lint").TaskType);
    }
}
