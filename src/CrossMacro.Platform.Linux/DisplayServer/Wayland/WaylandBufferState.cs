
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandBufferState : IDisposable
{
    private GCHandle _dispatcherHandle;
    private bool _disposed;

    public WaylandBufferState()
    {
        var dispatcher = (BufferDispatcher)Dispatch;
        _dispatcherHandle = GCHandle.Alloc(dispatcher, GCHandleType.Normal);
        DispatcherPtr = Marshal.GetFunctionPointerForDelegate(dispatcher);
    }

    private delegate int BufferDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    public IntPtr DispatcherPtr { get; }
    public bool Released { get; private set; } = true;

    public void MarkSubmitted() => Released = false;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_dispatcherHandle.IsAllocated)
        {
            _dispatcherHandle.Free();
        }
    }

    private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        if (opcode == 0)
        {
            Released = true;
        }

        return 0;
    }
}
