namespace CrossMacro.Daemon.Tests.Services;


public sealed class VirtualDeviceManagerTests
{
    [LinuxFact]
    public async Task Configure_WhenResolutionIsUnchanged_ReusesActiveDevice()
    {
        var devices = new List<FakeUInputDevice>();
        await using var manager = CreateManager(devices);

        await manager.ConfigureAsync(5120, 1440, CancellationToken.None);
        await manager.ConfigureAsync(5120, 1440, CancellationToken.None);

        var device = Assert.Single(devices);
        Assert.False(device.IsDisposed);
    }

    [LinuxFact]
    public async Task Configure_WhenResolutionChanges_ReplacesActiveDevice()
    {
        var devices = new List<FakeUInputDevice>();
        await using var manager = CreateManager(devices);

        await manager.ConfigureAsync(1920, 1080, CancellationToken.None);
        await manager.ConfigureAsync(5120, 1440, CancellationToken.None);

        Assert.Equal(2, devices.Count);
        Assert.True(devices[0].IsDisposed);
        Assert.False(devices[1].IsDisposed);
    }

    [LinuxFact]
    public async Task EnsureInitialized_WhenAbsoluteDeviceExists_PreservesActiveDevice()
    {
        var devices = new List<FakeUInputDevice>();
        await using var manager = CreateManager(devices);

        await manager.ConfigureAsync(5120, 1440, CancellationToken.None);
        await manager.EnsureInitializedAsync(CancellationToken.None);

        var device = Assert.Single(devices);
        Assert.False(device.IsDisposed);
    }

    [LinuxFact]
    public async Task EnsureInitialized_WhenNoDeviceExists_CreatesRelativeDeviceOnce()
    {
        var configurations = new List<(int Width, int Height)>();
        await using var manager = new VirtualDeviceManager((width, height, _) =>
        {
            configurations.Add((width, height));
            return Task.FromResult<IUInputDevice>(new FakeUInputDevice());
        });

        await manager.EnsureInitializedAsync(CancellationToken.None);
        await manager.EnsureInitializedAsync(CancellationToken.None);

        Assert.Equal([(0, 0)], configurations);
    }

    [LinuxFact]
    public async Task Configure_AfterReset_CreatesNewDeviceForSameResolution()
    {
        var devices = new List<FakeUInputDevice>();
        await using var manager = CreateManager(devices);

        await manager.ConfigureAsync(5120, 1440, CancellationToken.None);
        await manager.ResetAsync(CancellationToken.None);
        await manager.ConfigureAsync(5120, 1440, CancellationToken.None);

        Assert.Equal(2, devices.Count);
        Assert.True(devices[0].IsDisposed);
        Assert.False(devices[1].IsDisposed);
    }

    [LinuxFact]
    public async Task SendEvent_WhenNotConfigured_DoesNotThrow()
    {
        await using var manager = new VirtualDeviceManager();

        var ex = await Record.ExceptionAsync(() => manager.SendEventAsync(type: 1, code: 2, value: 3));

        Assert.Null(ex);
    }

    [LinuxFact]
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

    [LinuxFact]
    public async Task OperationsAfterDispose_ShouldThrowObjectDisposedException()
    {
        await using var manager = new VirtualDeviceManager();
        await manager.DisposeAsync();

        await TestAssertions.ThrowsAsync<ObjectDisposedException>(
            () => manager.SendEventAsync(type: 1, code: 2, value: 3));
    }

    [LinuxFact]
    public async Task Configure_WhenCanceledBeforeCreation_ShouldHonorCancellation()
    {
        await using var manager = new VirtualDeviceManager();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await TestAssertions.ThrowsAnyAsync<OperationCanceledException>(
            () => manager.ConfigureAsync(0, 0, cancellation.Token));
    }

    private static VirtualDeviceManager CreateManager(ICollection<FakeUInputDevice> devices)
    {
        return new VirtualDeviceManager((_, _, _) =>
        {
            var device = new FakeUInputDevice();
            devices.Add(device);
            return Task.FromResult<IUInputDevice>(device);
        });
    }

    private sealed class FakeUInputDevice : IUInputDevice
    {
        public bool SupportsAbsoluteCoordinates => true;
        public bool IsDisposed { get; private set; }

        public void CreateVirtualInputDevice()
        {
        }

        public void Move(int dx, int dy)
        {
        }

        public void MoveAbsolute(int x, int y)
        {
        }

        public void EmitButton(int buttonCode, bool pressed)
        {
        }

        public void EmitKey(int keyCode, bool pressed)
        {
        }

        public void SendEvent(ushort type, ushort code, int value)
        {
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
