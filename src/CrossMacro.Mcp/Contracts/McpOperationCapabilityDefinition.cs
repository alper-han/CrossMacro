namespace CrossMacro.Mcp.Contracts;

public sealed record McpOperationCapabilityDefinition(
    string Operation,
    IReadOnlyList<McpCapability> Capabilities);
