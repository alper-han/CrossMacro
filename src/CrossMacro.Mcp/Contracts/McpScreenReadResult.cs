namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// A read-only pixel, color-wait, or color-search result.
/// </summary>
public sealed record McpScreenReadResult(
    McpToolOutcome Outcome,
    string Mode,
    McpScreenPoint? Point,
    string? Color,
    string? ExpectedColor,
    McpScreenRegion? Region,
    int? Tolerance,
    bool? Found,
    int? TimeoutMs,
    string? ProviderName);
