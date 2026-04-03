namespace TaskRunnerExtended.Tests;

[TestClass]
[TestCategory("Unit")]
public class SmokeTest
{
    [TestMethod]
    public void ProjectLoads()
    {
        // Smoke test: verifies the test project can reference the extension project
        Assert.IsNotNull(typeof(TaskRunnerExtendedExtension));
    }
}
