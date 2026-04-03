namespace TaskRunnerExtended.Services.Execution;

using System.Text.RegularExpressions;

/// <summary>
/// Detects errors and warnings in task output using common patterns.
/// Counts errors/warnings per task for status reporting.
/// </summary>
public partial class ProblemMatcher
{
    private int _errorCount;
    private int _warningCount;

    /// <summary>Number of errors detected in the output.</summary>
    public int ErrorCount => _errorCount;

    /// <summary>Number of warnings detected in the output.</summary>
    public int WarningCount => _warningCount;

    /// <summary>Whether any errors were detected.</summary>
    public bool HasErrors => _errorCount > 0;

    /// <summary>
    /// Analyzes a line of output and updates error/warning counts.
    /// Returns the severity if a problem was detected, or null.
    /// </summary>
    public ProblemSeverity? AnalyzeLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;

        // MSBuild / dotnet errors: "file(line,col): error CS1234: message"
        if (MsBuildErrorPattern().IsMatch(line))
        {
            Interlocked.Increment(ref _errorCount);
            return ProblemSeverity.Error;
        }

        if (MsBuildWarningPattern().IsMatch(line))
        {
            Interlocked.Increment(ref _warningCount);
            return ProblemSeverity.Warning;
        }

        // npm errors: "ERR!" or "npm error"
        if (line.Contains("ERR!", StringComparison.Ordinal) ||
            line.Contains("npm error", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _errorCount);
            return ProblemSeverity.Error;
        }

        // TypeScript errors: "error TS1234:"
        if (TypeScriptErrorPattern().IsMatch(line))
        {
            Interlocked.Increment(ref _errorCount);
            return ProblemSeverity.Error;
        }

        // Generic "error:" or "Error:" at line start
        if (line.StartsWith("error:", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Error:", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _errorCount);
            return ProblemSeverity.Error;
        }

        // Build failed
        if (line.Contains("Build FAILED", StringComparison.Ordinal) ||
            line.Contains("FAILED", StringComparison.Ordinal) && line.Contains("Error(s)", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _errorCount);
            return ProblemSeverity.Error;
        }

        return null;
    }

    /// <summary>
    /// Returns a summary string like "2 errors, 3 warnings" or empty if clean.
    /// </summary>
    public string GetSummary()
    {
        if (_errorCount == 0 && _warningCount == 0) return string.Empty;

        var parts = new List<string>();
        if (_errorCount > 0) parts.Add($"{_errorCount} error(s)");
        if (_warningCount > 0) parts.Add($"{_warningCount} warning(s)");
        return string.Join(", ", parts);
    }

    /// <summary>Resets all counters.</summary>
    public void Reset()
    {
        _errorCount = 0;
        _warningCount = 0;
    }

    // MSBuild: "file(line,col): error CS1234: message" or "file : error CS1234: message"
    [GeneratedRegex(@":\s*error\s+\w+\d+\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex MsBuildErrorPattern();

    [GeneratedRegex(@":\s*warning\s+\w+\d+\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex MsBuildWarningPattern();

    // TypeScript: "error TS1234:"
    [GeneratedRegex(@"error\s+TS\d+:", RegexOptions.IgnoreCase)]
    private static partial Regex TypeScriptErrorPattern();
}

public enum ProblemSeverity
{
    Warning,
    Error,
}
