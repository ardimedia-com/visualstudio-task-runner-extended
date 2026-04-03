namespace TaskRunnerExtended.Tests.Discovery;

using TaskRunnerExtended.Models;
using TaskRunnerExtended.Services.Discovery;

[TestClass]
[TestCategory("Unit")]
public class CsprojDiscovererTests
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
    public async Task DiscoverAsync_NoCsproj_ReturnsEmpty()
    {
        var discoverer = new CsprojDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task DiscoverAsync_SdkStyleWithExecTargets_ReturnsTasks()
    {
        File.WriteAllText(Path.Combine(_tempDir, "MyApp.csproj"), """
        <Project Sdk="Microsoft.NET.Sdk.Web">
          <Target Name="WatchTailwindCss" BeforeTargets="Build"
                  Condition="'$(Configuration)' == 'Debug'">
            <Exec Command="npm run watchcss" WorkingDirectory="$(ProjectDir)" />
          </Target>
          <Target Name="BuildTailwindCss" BeforeTargets="Build"
                  Condition="'$(Configuration)' != 'Debug'">
            <Exec Command="npm run buildcss" />
          </Target>
        </Project>
        """);

        var discoverer = new CsprojDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual(2, result.Count);

        var watch = result.First(t => t.Label == "msbuild: WatchTailwindCss");
        Assert.AreEqual("dotnet", watch.Command);
        Assert.IsTrue(watch.Metadata?.Contains("Pre-Build"));
        Assert.IsTrue(watch.Metadata?.Contains("Debug"));

        var build = result.First(t => t.Label == "msbuild: BuildTailwindCss");
        Assert.AreEqual("dotnet", build.Command);
    }

    [TestMethod]
    public async Task DiscoverAsync_NetFrameworkStyle_DetectedCorrectly()
    {
        File.WriteAllText(Path.Combine(_tempDir, "LegacyApp.csproj"), """
        <?xml version="1.0" encoding="utf-8"?>
        <Project ToolsVersion="15.0" DefaultTargets="Build"
                 xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
          <Target Name="BeforeBuild">
            <Exec Command="npm run buildcss" />
          </Target>
        </Project>
        """);

        var discoverer = new CsprojDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual(1, result.Count);
        var task = result[0];
        Assert.AreEqual("msbuild: BeforeBuild", task.Label);
        Assert.AreEqual("msbuild.exe", task.Command);
        Assert.IsTrue(task.Metadata?.Contains(".NET Framework"));
        Assert.IsTrue(task.Metadata?.Contains("Pre-Build"));
    }

    [TestMethod]
    public async Task DiscoverAsync_NoExecTargets_ReturnsEmpty()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Library.csproj"), """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """);

        var discoverer = new CsprojDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task DiscoverAsync_MalformedXml_ReturnsError()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Bad.csproj"), "<<< not xml >>>");

        var discoverer = new CsprojDiscoverer();
        var result = await discoverer.DiscoverAsync(_tempDir, CancellationToken.None);

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result[0].IsError);
        Assert.IsTrue(result[0].Label.StartsWith("Parse Error:"));
    }
}
