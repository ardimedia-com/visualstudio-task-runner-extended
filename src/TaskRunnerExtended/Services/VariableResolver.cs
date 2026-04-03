namespace TaskRunnerExtended.Services;

using System.Text.RegularExpressions;

/// <summary>
/// Resolves VS Code-style variables in task commands and arguments.
/// Phase 1: ${workspaceFolder} only.
/// Phase 2: ${file}, ${env:VARIABLE}, etc.
/// </summary>
public static partial class VariableResolver
{
    [GeneratedRegex(@"\$\{workspaceFolder\}", RegexOptions.None)]
    private static partial Regex WorkspaceFolderPattern();

    /// <summary>
    /// Resolves variables in the given string.
    /// </summary>
    /// <param name="input">String potentially containing ${workspaceFolder} and other variables.</param>
    /// <param name="workspaceFolder">The workspace root directory (solution or .git folder).</param>
    /// <returns>String with variables replaced by their values.</returns>
    public static string Resolve(string input, string workspaceFolder)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains("${"))
        {
            return input;
        }

        var result = WorkspaceFolderPattern().Replace(input, workspaceFolder);

        // TODO Phase 2: Add ${file}, ${env:VARIABLE}, ${fileBasename}, etc.

        return result;
    }

    /// <summary>
    /// Resolves variables in a command and all its arguments.
    /// </summary>
    public static (string command, string[] args) ResolveCommand(
        string command, string[] args, string workspaceFolder)
    {
        return (
            Resolve(command, workspaceFolder),
            args.Select(a => Resolve(a, workspaceFolder)).ToArray());
    }
}
