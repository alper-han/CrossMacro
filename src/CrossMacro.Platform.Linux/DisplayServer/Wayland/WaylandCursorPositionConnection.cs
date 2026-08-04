namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandCursorPositionConnection : IDisposable
{
    private static readonly TimeSpan SetupTimeout = TimeSpan.FromSeconds(2);
    private const int SessionRoundtripLimit = 3;

    private readonly WaylandWlrConnection _connection;
    private readonly List<WaylandExtCursorOutputSession> _sessions = [];
    private readonly Dictionary<uint, int> _outputGenerations = [];
    private IntPtr _pointer;
    private int _registryGeneration;
    private bool _disposed;

    private WaylandCursorPositionConnection(
        WaylandWlrConnection connection,
        IntPtr pointer,
        ScreenRect desktopBounds)
    {
        _connection = connection;
        _pointer = pointer;
        DesktopBounds = desktopBounds;
    }

    public ScreenRect DesktopBounds { get; }

    public static WaylandCursorPositionConnection Connect(
        Action<int, int> positionChanged,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(positionChanged);

        var options = new ScreenReadOptions(timeout: SetupTimeout, cancellationToken: cancellationToken);
        var connection = WaylandWlrConnection.Connect(options);
        IntPtr pointer = IntPtr.Zero;
        WaylandCursorPositionConnection? cursorConnection = null;
        try
        {
            var registry = connection.Registry;
            if (registry.ExtOutputSourceManager == IntPtr.Zero || registry.ExtCopyManager == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "Wayland compositor does not expose ext-image-copy cursor capture globals.");
            }

            if (registry.Seat == IntPtr.Zero || (registry.SeatCapabilities & 1u) is 0)
            {
                throw new InvalidOperationException("Wayland compositor did not expose a pointer-capable wl_seat.");
            }

            pointer = connection.CreatePointer();
            if (pointer == IntPtr.Zero)
            {
                throw new InvalidOperationException("wl_seat.get_pointer returned NULL.");
            }

            cursorConnection = new WaylandCursorPositionConnection(
                connection,
                pointer,
                connection.GetVirtualScreenBounds());
            foreach (var output in registry.Outputs.Where(static output =>
                         output.ModeWidth > 0 && output.ModeHeight > 0))
            {
                cursorConnection._sessions.Add(connection.CreateCursorOutputSession(
                    output,
                    pointer,
                    positionChanged));
            }

            if (cursorConnection._sessions.Count is 0)
            {
                throw new InvalidOperationException("Wayland compositor did not expose usable outputs.");
            }

            var setupCancellation = new WaylandCaptureCancellation(options);
            for (int index = 0;
                 index < SessionRoundtripLimit && cursorConnection._sessions.Exists(static session => !session.IsReady);
                 index++)
            {
                connection.DisplayRoundtrip(setupCancellation);
            }

            if (cursorConnection._sessions.Exists(static session => !session.IsReady))
            {
                throw new InvalidOperationException(
                    "Wayland cursor capture sessions did not report usable buffer geometry.");
            }

            foreach (var session in cursorConnection._sessions)
            {
                session.CaptureOutputGeneration();
            }

            cursorConnection._registryGeneration = registry.Generation;
            cursorConnection.CaptureOutputGenerations();

            return cursorConnection;
        }
        catch
        {
            if (cursorConnection is not null)
            {
                cursorConnection.Dispose();
            }
            else
            {
                if (pointer != IntPtr.Zero)
                {
                    connection.DestroyPointer(pointer);
                }

                connection.Dispose();
            }

            throw;
        }
    }

    public void Dispatch(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var cancellation = new WaylandCaptureCancellation(
            new ScreenReadOptions(cancellationToken: cancellationToken));
        _connection.DisplayDispatch(cancellation);
        if (_connection.Registry.Generation != _registryGeneration)
        {
            throw new IOException("Wayland output or protocol topology changed; cursor sessions must be recreated.");
        }

        if (_sessions.Exists(static session => session.CaptureStopped) || OutputGeometryChanged())
        {
            throw new IOException("Wayland output geometry changed; cursor sessions must be recreated.");
        }
    }

    private void CaptureOutputGenerations()
    {
        _outputGenerations.Clear();
        foreach (var output in _connection.Registry.Outputs)
        {
            _outputGenerations[output.GlobalName] = output.Generation;
        }
    }

    private bool OutputGeometryChanged()
    {
        var outputs = _connection.Registry.Outputs;
        if (outputs.Count != _outputGenerations.Count)
        {
            return true;
        }

        return outputs.Exists(output =>
            !_outputGenerations.TryGetValue(output.GlobalName, out int generation) ||
            generation != output.Generation);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var session in _sessions)
        {
            session.Dispose();
        }

        _sessions.Clear();
        if (_pointer != IntPtr.Zero)
        {
            _connection.DestroyPointer(_pointer);
            _pointer = IntPtr.Zero;
        }

        _connection.Dispose();
    }
}
