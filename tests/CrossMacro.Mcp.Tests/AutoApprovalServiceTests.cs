namespace CrossMacro.Mcp.Tests;

public sealed class AutoApprovalServiceTests
{
    [Fact]
    public async Task RequestAsync_ApprovesAfterThePolicyBoundary()
    {
        var service = new AutoApprovalService();

        var result = await service.RequestAsync(
            new ApprovalRequest(
                "command.execute",
                "Execute a permitted command",
                TimeSpan.FromSeconds(30),
                "A permitted CrossMacro command.",
                ["CommandExecute"]),
            CancellationToken.None);

        Assert.Equal(ApprovalResult.Approved, result);
    }

    [Fact]
    public async Task RequestAsync_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new AutoApprovalService().RequestAsync(
                new ApprovalRequest("command.execute", "Command", TimeSpan.FromSeconds(30)),
             cancellation.Token));
    }

    [Fact]
    public async Task RequestAsync_DeniesPrivilegeElevationEvenWhenCapabilityPolicyAlreadyAcceptedIt()
    {
        var result = await new AutoApprovalService().RequestAsync(
            new ApprovalRequest(
                "setup.run",
                "Run temporary setup",
                TimeSpan.FromSeconds(30),
                "Temporary input setup",
                [nameof(McpCapability.PrivilegeElevation)]),
            CancellationToken.None);

        Assert.Equal(ApprovalResult.Denied, result);
    }
}
