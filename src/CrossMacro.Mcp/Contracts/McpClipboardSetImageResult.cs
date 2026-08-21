namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// The result of setting a validated PNG clipboard image without returning image bytes.
/// </summary>
public sealed record McpClipboardSetImageResult(
    McpToolOutcome Outcome,
    int? Width,
    int? Height,
    int? PngByteCount,
    long MaximumPngBytes);
