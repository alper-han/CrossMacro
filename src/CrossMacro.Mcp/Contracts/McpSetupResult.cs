namespace CrossMacro.Mcp.Contracts;

public sealed record McpSetupResult(
    string Action,
    McpToolOutcome Outcome,
    bool Applicable,
    string Provider,
    bool ShouldPrompt,
    bool Executed);
