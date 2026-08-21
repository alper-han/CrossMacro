namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// A bounded, deterministic listing of macro files in one directory.
/// </summary>
public sealed record McpMacroListResult(
    string DirectoryPath,
    IReadOnlyList<McpMacroFile> Macros,
    bool IsTruncated,
    McpToolOutcome Outcome);
