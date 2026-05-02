using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Infrastructure.Linux.Native.UInput;
using CrossMacro.Platform.Abstractions;
using CrossMacro.TestInfrastructure;
using Xunit;

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
            new(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0)
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

        Assert.Throws<InvalidOperationException>(() => simulator.SimulateBatch(steps));
    }

    [LinuxFact]
    public void SimulateBatch_WhenBatchDelayExceedsLimit_ShouldThrowBeforeSendingEvents()
    {
        var device = new FakeUInputDevice();
        using var simulator = new LinuxInputSimulator((_, _) => device);
        simulator.Initialize();

        InputSimulationStep[] steps = [new(UInputNative.EV_KEY, 30, 1, IpcProtocol.MaxSimulationBatchDelayMs + 1)];

        Assert.Throws<ArgumentOutOfRangeException>(() => simulator.SimulateBatch(steps));
        Assert.Empty(device.SentEvents);
    }

    [LinuxFact]
    public void SimulateBatch_WhenTotalDelayExceedsLimit_ShouldThrowBeforeSendingEvents()
    {
        var device = new FakeUInputDevice();
        using var simulator = new LinuxInputSimulator((_, _) => device);
        simulator.Initialize();

        InputSimulationStep[] steps =
        [
            new(UInputNative.EV_KEY, 30, 1, IpcProtocol.MaxSimulationBatchTotalDelayMs),
            new(UInputNative.EV_KEY, 30, 0, 1)
        ];

        Assert.Throws<ArgumentOutOfRangeException>(() => simulator.SimulateBatch(steps));
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
            SentEvents.Add((type, code, value));
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
