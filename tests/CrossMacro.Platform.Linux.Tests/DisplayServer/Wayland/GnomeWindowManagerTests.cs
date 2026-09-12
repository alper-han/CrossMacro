namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class GnomeWindowManagerTests
{
    [Fact]
    public async Task QueryOperations_WhenCancellationIsAlreadyRequested_CancelBeforeOpeningSessionBus()
    {
        await using var manager = new GnomeWindowManager();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.GetActiveWindowAsync(cancellation.Token));
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.GetWindowsAsync(cancellation.Token));
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.GetActiveWorkspaceAsync(cancellation.Token));
    }
}
