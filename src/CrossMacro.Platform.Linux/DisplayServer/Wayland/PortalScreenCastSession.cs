
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class PortalScreenCastSession : IDisposable
{
    private readonly IDisposable? _owner;
    private bool _disposed;
    private int _closed;

    public PortalScreenCastSession(
        string sessionHandle,
        IReadOnlyList<PortalStreamDescriptor> streams,
        SafeFileHandle pipeWireRemote,
        IDisposable? owner = null,
        string? restoreToken = null,
        string? restoreData = null)
    {
        ArgumentNullException.ThrowIfNull(streams);
        if (string.IsNullOrWhiteSpace(sessionHandle))
        {
            throw new ArgumentException("Portal sessions require a handle.", nameof(sessionHandle));
        }

        if (streams.Count is 0)
        {
            throw new ArgumentException("Portal sessions require at least one stream.", nameof(streams));
        }

        SessionHandle = sessionHandle;
        Streams = streams;
        PipeWireRemote = pipeWireRemote ?? throw new ArgumentNullException(nameof(pipeWireRemote));
        RestoreToken = string.IsNullOrWhiteSpace(restoreToken) ? null : restoreToken;
        RestoreData = string.IsNullOrWhiteSpace(restoreData) ? null : restoreData;
        _owner = owner;
    }

    public string SessionHandle { get; }

    public IReadOnlyList<PortalStreamDescriptor> Streams { get; }

    public SafeFileHandle PipeWireRemote { get; }

    public string? RestoreToken { get; }

    public string? RestoreData { get; }

    public bool IsClosed => Volatile.Read(ref _closed) is not 0;

    public PortalStreamDescriptor PrimaryStream => Streams[0];

    internal void MarkClosed() => Interlocked.Exchange(ref _closed, 1);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        MarkClosed();
        PipeWireRemote.Dispose();
        _owner?.Dispose();
    }
}
