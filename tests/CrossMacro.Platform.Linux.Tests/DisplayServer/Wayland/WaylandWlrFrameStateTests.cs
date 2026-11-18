
namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandWlrFrameStateTests
{
    [Fact]
    public void NewState_HasNoFrameMetadata_AndRetainsDispatcherPointer()
    {
        using var state = new WaylandWlrFrameState();

        Assert.NotEqual(IntPtr.Zero, state.DispatcherPtr);
        Assert.False(state.HasBuffer);
        Assert.False(state.BufferDone);
        Assert.False(state.CanCreateBuffer);
        Assert.False(state.Ready);
        Assert.False(state.Failed);
        Assert.Equal(0u, state.Format);
        Assert.Equal(0u, state.Width);
        Assert.Equal(0u, state.Height);
        Assert.Equal(0u, state.Stride);
        Assert.False(state.YInverted);
    }

    [Fact]
    public void Dispatcher_ExtractsConstraints_AndTracksFrameGates()
    {
        using var state = new WaylandWlrFrameState();
        var dispatch = GetDispatcher(state.DispatcherPtr);
        using var arguments = new WlArgumentPack(4);
        arguments[0] = new WlArgument { u = 875713112 };
        arguments[1] = new WlArgument { u = 1920 };
        arguments[2] = new WlArgument { u = 1080 };
        arguments[3] = new WlArgument { u = 7680 };

        Assert.Equal(0, Invoke(dispatch, 4, IntPtr.Zero));
        Assert.False(state.HasBuffer);
        Assert.False(state.YInverted);

        Assert.Equal(0, Invoke(dispatch, 0, arguments.Address));
        Assert.True(state.HasBuffer);
        Assert.False(state.BufferDone);
        Assert.False(state.CanCreateBuffer);
        Assert.Equal(875713112u, state.Format);
        Assert.Equal(1920u, state.Width);
        Assert.Equal(1080u, state.Height);
        Assert.Equal(7680u, state.Stride);

        Assert.Equal(0, Invoke(dispatch, 6, IntPtr.Zero));
        Assert.True(state.BufferDone);
        Assert.True(state.CanCreateBuffer);

        Assert.Equal(0, Invoke(dispatch, 2, IntPtr.Zero));
        Assert.True(state.Ready);
        Assert.False(state.Failed);

        Assert.Equal(0, Invoke(dispatch, 3, IntPtr.Zero));
        Assert.True(state.Failed);
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(1u, true)]
    [InlineData(3u, true)]
    [InlineData(2u, false)]
    public void Dispatcher_ReadsTheYInvertFlag(uint flags, bool expectedYInverted)
    {
        using var state = new WaylandWlrFrameState();
        var dispatch = GetDispatcher(state.DispatcherPtr);
        using var arguments = new WlArgumentPack(1);
        arguments[0] = new WlArgument { u = flags };

        Assert.Equal(0, Invoke(dispatch, 1, arguments.Address));

        Assert.Equal(expectedYInverted, state.YInverted);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var state = new WaylandWlrFrameState();

        var exception = Record.Exception(() =>
        {
            state.Dispose();
            state.Dispose();
        });

        Assert.Null(exception);
    }

    private static WaylandWlrFrameState.FrameDispatcher GetDispatcher(IntPtr pointer) =>
        Marshal.GetDelegateForFunctionPointer<WaylandWlrFrameState.FrameDispatcher>(pointer);

    private static int Invoke(WaylandWlrFrameState.FrameDispatcher dispatch, uint opcode, IntPtr args) =>
        dispatch(IntPtr.Zero, IntPtr.Zero, opcode, IntPtr.Zero, args);
}
