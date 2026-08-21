namespace CrossMacro.Mcp.Contracts;

public sealed record McpHelpResult(
    string Transport,
    string RuntimeRule,
    string SafetyNote,
    IReadOnlyList<McpAvailableTool> AvailableTools,
    bool IsRestricted = false);
