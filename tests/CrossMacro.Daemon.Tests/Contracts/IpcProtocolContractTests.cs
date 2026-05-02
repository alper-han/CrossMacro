using System.Runtime.InteropServices;
using CrossMacro.Daemon.Contracts.Ipc;

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
        Assert.Equal(3, IpcProtocol.ProtocolVersion);
        Assert.Equal("/run/crossmacro/crossmacro.sock", IpcProtocol.DefaultSocketPath);
        Assert.Equal(4096, IpcProtocol.MaxSimulationBatchEvents);
        Assert.Equal(1000, IpcProtocol.MaxSimulationBatchDelayMs);
        Assert.Equal(5000, IpcProtocol.MaxSimulationBatchTotalDelayMs);
    }

    [Fact]
    public void IpcInputEvent_ShouldKeepPackedSequentialLayout()
    {
        Assert.Equal(17, Marshal.SizeOf<IpcInputEvent>());
        Assert.Equal(0, Marshal.OffsetOf<IpcInputEvent>(nameof(IpcInputEvent.Type)).ToInt32());
        Assert.Equal(1, Marshal.OffsetOf<IpcInputEvent>(nameof(IpcInputEvent.Code)).ToInt32());
        Assert.Equal(5, Marshal.OffsetOf<IpcInputEvent>(nameof(IpcInputEvent.Value)).ToInt32());
        Assert.Equal(9, Marshal.OffsetOf<IpcInputEvent>(nameof(IpcInputEvent.Timestamp)).ToInt32());
    }

    [Fact]
    public void IpcInputEvent_ShouldStoreAssignedValues()
    {
        var evt = new IpcInputEvent
        {
            Type = 1,
            Code = 30,
            Value = 1,
            Timestamp = 123456789
        };

        Assert.Equal((byte)1, evt.Type);
        Assert.Equal(30, evt.Code);
        Assert.Equal(1, evt.Value);
        Assert.Equal(123456789, evt.Timestamp);
    }

    [Fact]
    public void IpcSimulationRequest_ShouldKeepPackedSequentialLayout()
    {
        Assert.Equal(12, Marshal.SizeOf<IpcSimulationRequest>());
        Assert.Equal(0, Marshal.OffsetOf<IpcSimulationRequest>(nameof(IpcSimulationRequest.Type)).ToInt32());
        Assert.Equal(2, Marshal.OffsetOf<IpcSimulationRequest>(nameof(IpcSimulationRequest.Code)).ToInt32());
        Assert.Equal(4, Marshal.OffsetOf<IpcSimulationRequest>(nameof(IpcSimulationRequest.Value)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<IpcSimulationRequest>(nameof(IpcSimulationRequest.DelayAfterMs)).ToInt32());
    }

    [Fact]
    public void IpcSimulationRequest_ShouldStoreAssignedValues()
    {
        var request = new IpcSimulationRequest
        {
            Type = 2,
            Code = 15,
            Value = -1,
            DelayAfterMs = 25
        };

        Assert.Equal((ushort)2, request.Type);
        Assert.Equal((ushort)15, request.Code);
        Assert.Equal(-1, request.Value);
        Assert.Equal(25, request.DelayAfterMs);
    }
}
