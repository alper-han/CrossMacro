
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class PortalScreenCastSession : IDisposable
{
    private readonly IDisposable? _owner;
    private bool _disposed;

    public PortalScreenCastSession(
        string sessionHandle,
        IReadOnlyList<PortalStreamDescriptor> streams,
        SafeFileHandle pipeWireRemote,
        IDisposable? owner = null,
        string? restoreToken = null)
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
        _owner = owner;
    }

    public string SessionHandle { get; }

    public IReadOnlyList<PortalStreamDescriptor> Streams { get; }

    public SafeFileHandle PipeWireRemote { get; }

    public string? RestoreToken { get; }

    public PortalStreamDescriptor PrimaryStream => Streams[0];

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PipeWireRemote.Dispose();
        _owner?.Dispose();
    }
}
