using System.Reflection;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandBufferStateTests
{
    [Fact]
    public void NewState_IsReleased_AndRetainsDispatcherPointer()
    {
        using var state = new WaylandBufferState();

        Assert.True(state.Released);
        Assert.NotEqual(IntPtr.Zero, state.DispatcherPtr);
    }

    [Fact]
    public void Dispatcher_TracksSubmissionAndOnlyOpcodeZeroRelease()
    {
        using var state = new WaylandBufferState();
        var dispatch = Marshal.GetDelegateForFunctionPointer(
            state.DispatcherPtr,
            typeof(WaylandBufferState).GetNestedType("BufferDispatcher", BindingFlags.NonPublic)!);

        state.MarkSubmitted();
        Assert.False(state.Released);

        Assert.Equal(0, Invoke(dispatch, 1));
        Assert.False(state.Released);

        Assert.Equal(0, Invoke(dispatch, 0));
        Assert.True(state.Released);

        state.MarkSubmitted();
        Assert.False(state.Released);
        Assert.Equal(0, Invoke(dispatch, 0));
        Assert.True(state.Released);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var state = new WaylandBufferState();

        var exception = Record.Exception(() =>
        {
            state.Dispose();
            state.Dispose();
        });

        Assert.Null(exception);
    }

    private static int Invoke(Delegate dispatch, uint opcode) =>
        (int)dispatch.DynamicInvoke(IntPtr.Zero, IntPtr.Zero, opcode, IntPtr.Zero, IntPtr.Zero)!;
}
