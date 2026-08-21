namespace CrossMacro.Mcp.Services;

/// <summary>
/// Approves effectful MCP requests after the capability, path, command, and
/// platform policies have accepted them. The approval service is not the
/// authorization boundary; those policies remain mandatory before this point.
/// </summary>
public sealed class AutoApprovalService : IApprovalService
{
    public Task<ApprovalResult> RequestAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.CapabilityNames.Contains(nameof(McpCapability.PrivilegeElevation), StringComparer.Ordinal))
        {
            return Task.FromResult(ApprovalResult.Denied);
        }

        return Task.FromResult(ApprovalResult.Approved);
    }
}
