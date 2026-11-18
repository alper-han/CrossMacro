
namespace CrossMacro.Daemon.Tests.Contracts;

public sealed class IpcProtocolContractTests
{
    [Fact]
    public void OpCodes_ShouldKeepExpectedWireValues()
    {
        Assert.Equal(0x01, (byte)IpcOpCode.Handshake);
        Assert.Equal(0x02, (byte)IpcOpCode.StartCapture);
        Assert.Equal(0x03, (byte)IpcOpCode.StopCapture);
        Assert.Equal(0x04, (byte)IpcOpCode.InputEvent);
        Assert.Equal(0x05, (byte)IpcOpCode.SimulateEvent);
        Assert.Equal(0x06, (byte)IpcOpCode.ConfigureResolution);
        Assert.Equal(0x07, (byte)IpcOpCode.CaptureStarted);
        Assert.Equal(0x08, (byte)IpcOpCode.CaptureStartFailed);
        Assert.Equal(0x09, (byte)IpcOpCode.SimulateEventBatch);
        Assert.Equal(0x0A, (byte)IpcOpCode.SimulationBatchCompleted);
        Assert.Equal(0x0B, (byte)IpcOpCode.SimulationBatchFailed);
        Assert.Equal(0xFF, (byte)IpcOpCode.Error);
    }

    [Fact]
    public void ProtocolConstants_ShouldKeepExpectedWireValues()
    {
        Assert.Equal(4, IpcProtocol.ProtocolVersion);
        Assert.Equal("/run/crossmacro/crossmacro.sock", IpcProtocol.DefaultSocketPath);
        Assert.Equal(4096, IpcProtocol.MaxSimulationBatchEvents);
        Assert.Equal(1_000_000, IpcProtocol.MaxSimulationBatchDelayMicroseconds);
        Assert.Equal(5_000_000, IpcProtocol.MaxSimulationBatchTotalDelayMicroseconds);
    }
}
