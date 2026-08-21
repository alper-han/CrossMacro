namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// The result of setting text clipboard content without echoing that content.
/// </summary>
public sealed record McpClipboardSetTextResult(
    McpToolOutcome Outcome,
    int? Length,
    int MaximumCharacters);
