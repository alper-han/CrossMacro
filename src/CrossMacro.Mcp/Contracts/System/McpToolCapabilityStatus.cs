namespace CrossMacro.Mcp.Contracts.System;

public sealed record McpToolCapabilityStatus(
    string Operation,
    IReadOnlyList<string> RequiredCapabilities,
    bool Enabled);
