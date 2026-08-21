namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// A bounded result from a read-only window query.
/// </summary>
public sealed record McpWindowQueryResult(
    McpToolOutcome Outcome,
    string Mode,
    IReadOnlyList<McpWindowInfo> Windows,
    int TotalCount,
    bool IsTruncated,
    bool? Found,
    int? TimeoutMs);
