namespace CrossMacro.Mcp.Services;

public sealed record McpAuditEntry(
    DateTimeOffset Timestamp,
    string ToolName,
    string Access,
    string Approval,
    string Result,
    string? OperationId = null,
    IReadOnlyList<string>? capabilityNames = null,
    string? RuntimeIdentity = null,
    string? RedactedTarget = null)
{
    public IReadOnlyList<string> Capabilities { get; init; } = capabilityNames ?? [];
}
