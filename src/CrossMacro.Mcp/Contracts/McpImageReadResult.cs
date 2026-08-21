namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// Validated PNG metadata. File content is emitted only as an MCP image content
/// block when explicitly requested.
/// </summary>
public sealed record McpImageReadResult(
    McpToolOutcome Outcome,
    int? Width,
    int? Height,
    bool ImageIncluded,
    int? PngByteCount,
    int MaximumInlineImageBytes);
