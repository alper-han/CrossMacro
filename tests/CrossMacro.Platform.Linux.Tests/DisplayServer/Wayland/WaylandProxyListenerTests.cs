namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandProxyListenerTests
{
    [Fact]
    public void DispatcherPointer_DeliversTargetOpcodeAndArguments_AndReportsHandled()
    {
        using var listener = new RecordingListener();
        var target = new IntPtr(0x1234);
        var args = new IntPtr(0x5678);

        var result = WaylandDispatcherInvoker.Invoke(listener.DispatcherPtr, 7, args, target);

        Assert.Equal(0, result);
        var delivered = Assert.Single(listener.Events);
        Assert.Equal((target, 7u, args), delivered);
    }

    [Fact]
    public void Listeners_HaveDistinctDispatcherPointers()
    {
        using var first = new RecordingListener();
        using var second = new RecordingListener();

        Assert.NotEqual(IntPtr.Zero, first.DispatcherPtr);
        Assert.NotEqual(first.DispatcherPtr, second.DispatcherPtr);
    }

    [Fact]
    public void Dispose_ReleasesTheSubclassOnce_AndIsIdempotent()
    {
        var listener = new RecordingListener();

        listener.Dispose();
        listener.Dispose();

        Assert.Equal(1, listener.DisposedCount);
        Assert.True(listener.WasDisposedWhenReleased);
    }

    [Fact]
    public void CallbackListener_ForwardsOpcodeAndArguments()
    {
        var received = new List<(uint Opcode, IntPtr Args)>();
        using var listener = new WaylandCallbackListener((opcode, args) => received.Add((opcode, args)));

        var result = WaylandDispatcherInvoker.Invoke(listener.DispatcherPtr, 3, new IntPtr(42));

        Assert.Equal(0, result);
        Assert.Equal([(3u, new IntPtr(42))], received);
    }

    private sealed class RecordingListener : WaylandProxyListener
    {
        public List<(IntPtr Target, uint Opcode, IntPtr Args)> Events { get; } = [];
        public int DisposedCount { get; private set; }
        public bool WasDisposedWhenReleased { get; private set; }

        protected override void OnEvent(IntPtr target, uint opcode, IntPtr args) => Events.Add((target, opcode, args));

        protected override void OnDisposed()
        {
            DisposedCount++;
            WasDisposedWhenReleased = IsDisposed;
        }
    }
}
