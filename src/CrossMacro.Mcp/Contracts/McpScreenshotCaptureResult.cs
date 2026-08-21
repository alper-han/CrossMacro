namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// Bounded screenshot metadata. PNG content is emitted only as an MCP image
/// content block when the caller explicitly requests it.
/// </summary>
public sealed record McpScreenshotCaptureResult(
    McpToolOutcome Outcome,
    int? Width,
    int? Height,
    string? ProviderName,
    bool? IsRegion,
    string? OutputPath,
    bool? CopiedToClipboard,
    bool ImageIncluded,
    int? PngByteCount,
    int MaximumInlineImageBytes);
