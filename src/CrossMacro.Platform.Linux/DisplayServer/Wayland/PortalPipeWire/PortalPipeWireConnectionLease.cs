namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal sealed class PortalPipeWireConnectionLease(PortalPipeWireConnection connection, Action release) : IDisposable
{
    private readonly Action _release = release;
    private int _disposed;

    public PortalPipeWireConnection Connection { get; } = connection;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is 0)
        {
            _release();
        }
    }
}
