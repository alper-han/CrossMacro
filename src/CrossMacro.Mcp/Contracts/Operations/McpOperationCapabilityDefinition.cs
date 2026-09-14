namespace CrossMacro.Mcp.Contracts.Operations;

public sealed record McpOperationCapabilityDefinition(
    string Operation,
    IReadOnlyList<McpCapability> Capabilities);
