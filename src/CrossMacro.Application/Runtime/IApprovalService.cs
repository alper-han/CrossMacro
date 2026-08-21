namespace CrossMacro.Application.Runtime;

public interface IApprovalService
{
    public Task<ApprovalResult> RequestAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken);
}
