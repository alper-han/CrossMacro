namespace CrossMacro.Mcp.Services;

/// <summary>
/// Explicit denial adapter for hosts that want MCP effectful operations disabled.
/// The standalone CrossMacro composition uses <see cref="AutoApprovalService"/>
/// after capability, path, command, and platform policies have accepted a call.
/// </summary>
public sealed class DenyApprovalService : IApprovalService
{
    public Task<ApprovalResult> RequestAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ApprovalResult.Denied);
    }
}
