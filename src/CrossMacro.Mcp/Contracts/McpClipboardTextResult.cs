namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// A bounded text clipboard read. Text is present only for a successful read.
/// </summary>
public sealed record McpClipboardTextResult(
    McpToolOutcome Outcome,
    string? Text,
    int? Length,
    int MaximumCharacters);
