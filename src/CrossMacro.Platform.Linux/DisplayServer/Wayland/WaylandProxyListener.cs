namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Receives the events libwayland dispatches for one proxy. It owns the unmanaged callback:
/// the delegate stays pinned for as long as the proxy may deliver events and is released on disposal.
/// </summary>
internal abstract class WaylandProxyListener : IDisposable
{
    internal delegate int Dispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    private GCHandle _dispatcherHandle;

    protected WaylandProxyListener()
    {
        Dispatcher dispatcher = (_, target, opcode, _, args) =>
        {
            OnEvent(target, opcode, args);
            return 0;
        };
        _dispatcherHandle = GCHandle.Alloc(dispatcher, GCHandleType.Normal);
        DispatcherPtr = Marshal.GetFunctionPointerForDelegate(dispatcher);
    }

    /// <summary>The function pointer to register with <c>wl_proxy_add_dispatcher</c>.</summary>
    public IntPtr DispatcherPtr { get; }

    protected bool IsDisposed { get; private set; }

    /// <summary>
    /// Handles one event of the proxy <paramref name="target"/>.
    /// <paramref name="args"/> points at the event's <c>wl_argument</c> array.
    /// </summary>
    protected abstract void OnEvent(IntPtr target, uint opcode, IntPtr args);

    /// <summary>Releases what the subclass holds. Runs once, after the callback has been unpinned.</summary>
    protected virtual void OnDisposed() { /* Most listeners hold nothing beyond the callback. */ }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        if (_dispatcherHandle.IsAllocated)
        {
            _dispatcherHandle.Free();
        }

        OnDisposed();
    }
}
