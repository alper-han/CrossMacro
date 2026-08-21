namespace CrossMacro.Mcp.Contracts;

public sealed record McpTextExpansionsResult(
    string Action,
    McpToolOutcome Outcome,
    IReadOnlyList<McpTextExpansion> Expansions,
    string? ProfileId,
    bool Found);
