namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// Metadata and validation diagnostics produced by macro inspection.
/// </summary>
public sealed record McpMacroInspectResult(
    McpToolOutcome Outcome,
    McpMacroInfo? Macro);
