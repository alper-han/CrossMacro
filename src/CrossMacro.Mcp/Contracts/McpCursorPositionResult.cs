namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// The current logical global cursor position when the active desktop provider exposes it.
/// </summary>
public sealed record McpCursorPositionResult(
    McpToolOutcome Outcome,
    McpScreenPoint? Point,
    string? ProviderName);
