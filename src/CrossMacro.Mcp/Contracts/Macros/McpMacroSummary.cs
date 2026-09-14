namespace CrossMacro.Mcp.Contracts.Macros;

/// <summary>
/// A compact macro validation summary.
/// </summary>
public sealed record McpMacroSummary(
    string MacroPath,
    string MacroName,
    int EventCount,
    long TotalDurationMs,
    string CoordinateMode,
    bool IsAbsoluteCoordinates);
