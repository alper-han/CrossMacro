namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// Read-only macro metadata, excluding events, script steps, and embedded assets.
/// </summary>
public sealed record McpMacroInfo(
    string MacroPath,
    string MacroName,
    DateTime CreatedAt,
    int EventCount,
    long TotalDurationMs,
    string CoordinateMode,
    bool IsAbsoluteCoordinates,
    bool SkipInitialZeroZero,
    long TrailingDelayMicroseconds,
    int TrailingDelayMs,
    bool HasTrailingRandomDelay,
    int TrailingDelayMinMs,
    int TrailingDelayMaxMs,
    McpMacroEventBreakdown EventBreakdown);
