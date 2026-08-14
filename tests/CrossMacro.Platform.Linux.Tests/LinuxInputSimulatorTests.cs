
namespace CrossMacro.Platform.Linux.Tests;

public sealed class LinuxInputSimulatorTests
{
    [LinuxFact]
    public void SupportsBatchedInput_BeforeInitialize_ShouldBeFalse()
    {
        using var simulator = new LinuxInputSimulator(static (_, _) => new FakeUInputDevice());

        Assert.False(simulator.SupportsBatchedInput);
    }

    [LinuxFact]
    public void SupportsBatchedInput_AfterInitialize_ShouldBeTrue()
    {
        using var simulator = new LinuxInputSimulator(static (_, _) => new FakeUInputDevice());

        simulator.Initialize();

        Assert.True(simulator.SupportsBatchedInput);
    }

    [LinuxFact]
    public async Task InitializeAsync_UsesAsynchronousDeviceCreation()
    {
        var device = new FakeUInputDevice();
        using var simulator = new LinuxInputSimulator((_, _) => device);

        await simulator.InitializeAsync(cancellationToken: CancellationToken.None);

        Assert.Equal(1, device.AsyncCreateCalls);
        Assert.Equal(0, device.SyncCreateCalls);
    }

    [LinuxFact]
    public async Task InitializeAsync_WhenCancelledDuringDeviceCreation_PropagatesCancellationAndDisposesDevice()
    {
        var device = new FakeUInputDevice
        {
            AsyncCreateHandler = static cancellationToken => Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, cancellationToken),
        };
        using var simulator = new LinuxInputSimulator((_, _) => device);
        using var cancellationSource = new CancellationTokenSource();

        var initialization = simulator.InitializeAsync(cancellationToken: cancellationSource.Token);
        await cancellationSource.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialization);

        Assert.Equal(cancellationSource.Token, device.AsyncCreateCancellationToken);
        Assert.True(device.Disposed);
    }

    [LinuxFact]
    public void SimulateBatch_WhenInitialized_ShouldSendEventsInOrder()
    {
        var device = new FakeUInputDevice();
        using var simulator = new LinuxInputSimulator((_, _) => device);
        simulator.Initialize();

        InputSimulationStep[] steps =
        [
            new(UInputNative.EV_KEY, 30, 1),
            new(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0, 1),
            new(UInputNative.EV_KEY, 30, 0),
            new(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0),
        ];

        simulator.SimulateBatch(steps);

        Assert.Equal(
            [(UInputNative.EV_KEY, (ushort)30, 1), (UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0), (UInputNative.EV_KEY, (ushort)30, 0), (UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0)],
            device.SentEvents);
    }

    [LinuxFact]
    public void SimulateBatch_WhenNotInitialized_ShouldThrow()
    {
        using var simulator = new LinuxInputSimulator(static (_, _) => new FakeUInputDevice());

        InputSimulationStep[] steps = [new(UInputNative.EV_KEY, 30, 1)];

        _ = Assert.Throws<InvalidOperationException>(() => simulator.SimulateBatch(steps));
    }

    [LinuxFact]
    public void SimulateBatch_WhenBatchDelayExceedsLimit_ShouldThrowBeforeSendingEvents()
    {
        var device = new FakeUInputDevice();
        using var simulator = new LinuxInputSimulator((_, _) => device);
        simulator.Initialize();

        InputSimulationStep[] steps = [new(UInputNative.EV_KEY, 30, 1, IpcProtocol.MaxSimulationBatchDelayMicroseconds + 1)];

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => simulator.SimulateBatch(steps));
        Assert.Empty(device.SentEvents);
    }

    [LinuxFact]
    public void SimulateBatch_ForwardsSubMillisecondDelayToHighResolutionWait()
    {
        var device = new FakeUInputDevice();
        long observedDelayMicroseconds = 0;
        using var simulator = new LinuxInputSimulator(
            (_, _) => device,
            delayMicroseconds => observedDelayMicroseconds = delayMicroseconds);
        simulator.Initialize();

        simulator.SimulateBatch(
        [
            new(UInputNative.EV_KEY, 30, 1, 500),
        ]);

        Assert.Equal(500, observedDelayMicroseconds);
    }

    [LinuxFact]
    public void SimulateBatch_WhenTotalDelayExceedsLimit_ShouldThrowBeforeSendingEvents()
    {
        var device = new FakeUInputDevice();
        using var simulator = new LinuxInputSimulator((_, _) => device);
        simulator.Initialize();

        InputSimulationStep[] steps =
        [
            new(UInputNative.EV_KEY, 30, 1, IpcProtocol.MaxSimulationBatchTotalDelayMicroseconds),
            new(UInputNative.EV_KEY, 30, 0, 1),
        ];

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => simulator.SimulateBatch(steps));
        Assert.Empty(device.SentEvents);
    }

    [LinuxFact]
    public void Dispose_ShouldDisposeUnderlyingDeviceAndDisableBatchSupport()
    {
        var device = new FakeUInputDevice();
        var simulator = new LinuxInputSimulator((_, _) => device);
        simulator.Initialize();

        simulator.Dispose();

        Assert.True(device.Disposed);
        Assert.False(simulator.SupportsBatchedInput);
    }

    private sealed class FakeUInputDevice : IUInputDevice
    {
        public List<(ushort Type, ushort Code, int Value)> SentEvents { get; } = new();

        public bool SupportsAbsoluteCoordinates => false;

        public bool Disposed { get; private set; }
        public int SyncCreateCalls { get; private set; }
        public int AsyncCreateCalls { get; private set; }
        public CancellationToken AsyncCreateCancellationToken { get; private set; }
        public Func<CancellationToken, Task>? AsyncCreateHandler { get; init; }

        public void CreateVirtualInputDevice()
        {
            SyncCreateCalls++;
        }

        public Task CreateVirtualInputDeviceAsync(CancellationToken cancellationToken = default)
        {
            AsyncCreateCalls++;
            AsyncCreateCancellationToken = cancellationToken;
            return AsyncCreateHandler?.Invoke(cancellationToken) ?? Task.CompletedTask;
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
            SentEvents.Add((type, code, value));
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
