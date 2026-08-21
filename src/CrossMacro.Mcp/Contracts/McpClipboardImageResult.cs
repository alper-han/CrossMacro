namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// A bounded PNG clipboard read. Image bytes are emitted only as an MCP image
/// content block when explicitly requested.
/// </summary>
public sealed record McpClipboardImageResult(
    McpToolOutcome Outcome,
    bool ImageAvailable,
    int? Width,
    int? Height,
    bool ImageIncluded,
    int? PngByteCount,
    int MaximumPngBytes,
    int MaximumInlineImageBytes);
