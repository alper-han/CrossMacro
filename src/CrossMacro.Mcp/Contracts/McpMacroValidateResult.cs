namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// Validation diagnostics produced without playing a macro.
/// </summary>
public sealed record McpMacroValidateResult(
    McpToolOutcome Outcome,
    McpMacroSummary? Macro);
