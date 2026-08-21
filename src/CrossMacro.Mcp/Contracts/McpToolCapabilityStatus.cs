namespace CrossMacro.Mcp.Contracts;

public sealed record McpToolCapabilityStatus(
    string Operation,
    IReadOnlyList<string> RequiredCapabilities,
    bool Enabled);
