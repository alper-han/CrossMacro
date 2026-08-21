namespace CrossMacro.Mcp.Contracts;

public sealed record McpTextExpansion(
    string Trigger,
    string Replacement,
    bool IsEnabled,
    string Method,
    string InsertionMode,
    string DirectTypingMethod);
