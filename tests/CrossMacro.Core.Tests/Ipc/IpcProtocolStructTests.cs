namespace CrossMacro.Core.Tests.Ipc;

using CrossMacro.Core.Ipc;

public class IpcProtocolStructTests
{
    [Fact]
    public void SocketPaths_ShouldBeNonEmptyUnixStylePaths()
    {
        Assert.StartsWith("/", IpcProtocol.DefaultSocketPath);
        Assert.StartsWith("/", IpcProtocol.FallbackSocketPath);
        Assert.NotEmpty(IpcProtocol.DefaultSocketPath);
        Assert.NotEmpty(IpcProtocol.FallbackSocketPath);
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
    public void IpcSimulationRequest_ShouldStoreAssignedValues()
    {
        var request = new IpcSimulationRequest
        {
            Type = 2,
            Code = 15,
            Value = -1
        };

        Assert.Equal((ushort)2, request.Type);
        Assert.Equal((ushort)15, request.Code);
        Assert.Equal(-1, request.Value);
    }
}
