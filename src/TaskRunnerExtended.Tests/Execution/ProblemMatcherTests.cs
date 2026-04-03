namespace TaskRunnerExtended.Tests.Execution;

using TaskRunnerExtended.Services.Execution;

[TestClass]
[TestCategory("Unit")]
public class ProblemMatcherTests
{
    [TestMethod]
    public void AnalyzeLine_MsBuildError_DetectsError()
    {
        var matcher = new ProblemMatcher();
        var result = matcher.AnalyzeLine("Program.cs(10,5): error CS1002: ; expected");
        Assert.AreEqual(ProblemSeverity.Error, result);
        Assert.AreEqual(1, matcher.ErrorCount);
    }

    [TestMethod]
    public void AnalyzeLine_MsBuildWarning_DetectsWarning()
    {
        var matcher = new ProblemMatcher();
        var result = matcher.AnalyzeLine("MyProject.csproj : warning NU1903: Package has vulnerability");
        Assert.AreEqual(ProblemSeverity.Warning, result);
        Assert.AreEqual(1, matcher.WarningCount);
    }

    [TestMethod]
    public void AnalyzeLine_TypeScriptError_DetectsError()
    {
        var matcher = new ProblemMatcher();
        var result = matcher.AnalyzeLine("src/app.ts(5,10): error TS2304: Cannot find name 'foo'.");
        Assert.AreEqual(ProblemSeverity.Error, result);
    }

    [TestMethod]
    public void AnalyzeLine_NpmError_DetectsError()
    {
        var matcher = new ProblemMatcher();
        var result = matcher.AnalyzeLine("npm ERR! code ELIFECYCLE");
        Assert.AreEqual(ProblemSeverity.Error, result);
    }

    [TestMethod]
    public void AnalyzeLine_BuildFailed_DetectsError()
    {
        var matcher = new ProblemMatcher();
        var result = matcher.AnalyzeLine("Build FAILED.");
        Assert.AreEqual(ProblemSeverity.Error, result);
    }

    [TestMethod]
    public void AnalyzeLine_NormalOutput_ReturnsNull()
    {
        var matcher = new ProblemMatcher();
        var result = matcher.AnalyzeLine("  Restored MyProject.csproj (in 1.2 sec).");
        Assert.IsNull(result);
        Assert.AreEqual(0, matcher.ErrorCount);
        Assert.AreEqual(0, matcher.WarningCount);
    }

    [TestMethod]
    public void GetSummary_MultipleProblems()
    {
        var matcher = new ProblemMatcher();
        matcher.AnalyzeLine("file.cs(1,1): error CS1234: msg");
        matcher.AnalyzeLine("file.cs(2,1): error CS5678: msg");
        matcher.AnalyzeLine("file.cs(3,1): warning CS0001: msg");

        Assert.AreEqual("2 error(s), 1 warning(s)", matcher.GetSummary());
    }

    [TestMethod]
    public void GetSummary_NoProblems_ReturnsEmpty()
    {
        var matcher = new ProblemMatcher();
        Assert.AreEqual(string.Empty, matcher.GetSummary());
    }

    [TestMethod]
    public void Reset_ClearsCounters()
    {
        var matcher = new ProblemMatcher();
        matcher.AnalyzeLine("file.cs(1,1): error CS1234: msg");
        matcher.Reset();
        Assert.AreEqual(0, matcher.ErrorCount);
        Assert.AreEqual(string.Empty, matcher.GetSummary());
    }
}
