namespace TaskRunnerExtended.Tests.Discovery;

using TaskRunnerExtended.Services;

[TestClass]
[TestCategory("Unit")]
public class VariableResolverTests
{
    [TestMethod]
    public void Resolve_WorkspaceFolder_Replaced()
    {
        var result = VariableResolver.Resolve("${workspaceFolder}/src", @"D:\Projects\MyApp");
        Assert.AreEqual(@"D:\Projects\MyApp/src", result);
    }

    [TestMethod]
    public void Resolve_NoVariables_Unchanged()
    {
        var result = VariableResolver.Resolve("dotnet build", @"D:\Projects\MyApp");
        Assert.AreEqual("dotnet build", result);
    }

    [TestMethod]
    public void Resolve_EmptyString_ReturnsEmpty()
    {
        var result = VariableResolver.Resolve("", @"D:\Projects\MyApp");
        Assert.AreEqual("", result);
    }

    [TestMethod]
    public void ResolveCommand_ResolvesCommandAndArgs()
    {
        var (cmd, args) = VariableResolver.ResolveCommand(
            "dotnet",
            ["build", "${workspaceFolder}/MyApp.csproj"],
            @"D:\Projects\MyApp");

        Assert.AreEqual("dotnet", cmd);
        Assert.AreEqual(@"D:\Projects\MyApp/MyApp.csproj", args[1]);
    }
}
