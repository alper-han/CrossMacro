namespace CrossMacro.Mcp.Contracts;

public sealed record McpAvailableTool(
    string Name,
    string Description,
    string Access,
    bool Enabled = true,
    IReadOnlyList<McpToolCapabilityStatus>? operationCapabilityStatuses = null)
{
    public IReadOnlyList<McpToolCapabilityStatus> OperationCapabilities { get; init; } = operationCapabilityStatuses ?? [];
}
