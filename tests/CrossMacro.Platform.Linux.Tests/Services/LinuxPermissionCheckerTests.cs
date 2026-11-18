namespace CrossMacro.Platform.Linux.Tests.Services;


public sealed class LinuxPermissionCheckerTests
{
    [Fact]
    public async Task CheckUInputAccessAsync_WhenCanceled_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var checker = new LinuxPermissionChecker();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => checker.CheckUInputAccessAsync(cancellation.Token).AsTask());
    }
}
