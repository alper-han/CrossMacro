namespace CrossMacro.Mcp.Contracts;

public sealed record McpToolDefinition(
    string Name,
    string Title,
    string Description,
    McpToolAccess Access,
    IReadOnlyList<McpCapability> Capabilities,
    McpCapabilityRequirement CapabilityRequirement = McpCapabilityRequirement.All,
    IReadOnlyList<McpOperationCapabilityDefinition>? operationCapabilityDefinitions = null)
{
    public IReadOnlyList<McpOperationCapabilityDefinition> OperationCapabilities { get; init; } = operationCapabilityDefinitions ?? [];
}
