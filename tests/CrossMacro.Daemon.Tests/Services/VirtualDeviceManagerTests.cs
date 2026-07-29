namespace CrossMacro.Daemon.Tests.Services;


public sealed class VirtualDeviceManagerTests
{
    [Fact]
    public async Task SendEvent_WhenNotConfigured_DoesNotThrow()
    {
        await using var manager = new VirtualDeviceManager();

        var ex = await Record.ExceptionAsync(() => manager.SendEventAsync(type: 1, code: 2, value: 3));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Reset_WhenNotConfigured_DoesNotThrow()
    {
        await using var manager = new VirtualDeviceManager();

        var ex = await Record.ExceptionAsync(() => manager.ResetAsync());

        Assert.Null(ex);
    }

    [Fact]
    public async Task Dispose_CanBeCalledMultipleTimes()
    {
        var manager = new VirtualDeviceManager();

        var ex = await Record.ExceptionAsync(async () =>
        {
            await manager.DisposeAsync();
            await manager.DisposeAsync();
        });

        Assert.Null(ex);
    }

    [Fact]
    public async Task OperationsAfterDispose_ShouldThrowObjectDisposedException()
    {
        await using var manager = new VirtualDeviceManager();
        await manager.DisposeAsync();

        await TestAssertions.ThrowsAsync<ObjectDisposedException>(
            () => manager.SendEventAsync(type: 1, code: 2, value: 3));
    }

    [Fact]
    public async Task Configure_WhenCanceledBeforeCreation_ShouldHonorCancellation()
    {
        await using var manager = new VirtualDeviceManager();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await TestAssertions.ThrowsAnyAsync<OperationCanceledException>(
            () => manager.ConfigureAsync(0, 0, cancellation.Token));
    }
}
