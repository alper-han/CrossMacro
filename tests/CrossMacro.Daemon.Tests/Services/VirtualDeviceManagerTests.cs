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
    public async Task SendEvent_WhenNotConfigured_ThrowsInsteadOfSilentlyDroppingInput()
    {
        await using var manager = new VirtualDeviceManager();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.SendEventAsync(type: 1, code: 2, value: 3, CancellationToken.None));

        Assert.Contains("not initialized", exception.Message, StringComparison.Ordinal);
    }

    [LinuxFact]
    public async Task Reset_WhenNotConfigured_DoesNotThrow()
    {
        await using var manager = new VirtualDeviceManager();

        var ex = await Record.ExceptionAsync(() => manager.ResetAsync(CancellationToken.None));

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
            () => manager.SendEventAsync(type: 1, code: 2, value: 3, CancellationToken.None));
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

    [LinuxFact]
    public async Task SendEvents_WhenUInputWriteFails_PropagatesFailureAndStopsTheBatch()
    {
        var devices = new List<FakeUInputDevice>();
        await using var manager = CreateManager(devices);
        await manager.ConfigureAsync(5120, 1440, CancellationToken.None);
        var device = Assert.Single(devices);
        device.ThrowOnSendEvent = new IOException("uinput event write failed: Errno=5.");

        IpcSimulationRequest[] batch =
        [
            new() { Type = UInputNative.EV_ABS, Code = UInputNative.ABS_X, Value = 123 },
            new() { Type = UInputNative.EV_ABS, Code = UInputNative.ABS_Y, Value = 456 },
        ];

        var exception = await Assert.ThrowsAsync<IOException>(
            () => manager.SendEventsAsync(batch, CancellationToken.None));

        Assert.Contains("Errno=5", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, device.SendEventCalls);
    }

    [LinuxFact]
    public async Task SendEvents_WhenAbsoluteTargetRepeats_ReassertsThroughAnAdjacentAbsolutePoint()
    {
        var devices = new List<FakeUInputDevice>();
        await using var manager = CreateManager(devices);
        await manager.ConfigureAsync(5120, 1440, CancellationToken.None);
        var device = Assert.Single(devices);

        await manager.SendEventsAsync(AbsoluteMove(4245, 346), CancellationToken.None);
        device.SentEvents.Clear();

        await manager.SendEventsAsync(AbsoluteMove(4245, 346), CancellationToken.None);

        Assert.Equal(
        [
            (UInputNative.EV_ABS, UInputNative.ABS_X, 4246),
            (UInputNative.EV_ABS, UInputNative.ABS_Y, 346),
            (UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0),
            (UInputNative.EV_ABS, UInputNative.ABS_X, 4245),
            (UInputNative.EV_ABS, UInputNative.ABS_Y, 346),
            (UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0),
        ],
        device.SentEvents);
    }

    [LinuxFact]
    public async Task SendEvents_WhenRepeatedAbsoluteTargetIsAtRightEdge_ReassertsThroughThePreviousPixel()
    {
        var devices = new List<FakeUInputDevice>();
        await using var manager = CreateManager(devices);
        await manager.ConfigureAsync(3, 2, CancellationToken.None);
        var device = Assert.Single(devices);

        await manager.SendEventsAsync(AbsoluteMove(2, 1), CancellationToken.None);
        device.SentEvents.Clear();

        await manager.SendEventsAsync(AbsoluteMove(2, 1), CancellationToken.None);

        Assert.Equal((UInputNative.EV_ABS, UInputNative.ABS_X, 1), device.SentEvents[0]);
        Assert.Equal((UInputNative.EV_ABS, UInputNative.ABS_Y, 1), device.SentEvents[1]);
        Assert.Equal((UInputNative.EV_ABS, UInputNative.ABS_X, 2), device.SentEvents[3]);
        Assert.Equal((UInputNative.EV_ABS, UInputNative.ABS_Y, 1), device.SentEvents[4]);
    }

    private static IpcSimulationRequest[] AbsoluteMove(int x, int y) =>
    [
        new() { Type = UInputNative.EV_ABS, Code = UInputNative.ABS_X, Value = x },
        new() { Type = UInputNative.EV_ABS, Code = UInputNative.ABS_Y, Value = y },
        new() { Type = UInputNative.EV_SYN, Code = UInputNative.SYN_REPORT, Value = 0 },
    ];

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
        public int SendEventCalls { get; private set; }
        public List<(ushort Type, ushort Code, int Value)> SentEvents { get; } = [];
        public Exception? ThrowOnSendEvent { get; set; }

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
            SendEventCalls++;
            if (ThrowOnSendEvent is not null)
            {
                throw ThrowOnSendEvent;
            }

            SentEvents.Add((type, code, value));
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
