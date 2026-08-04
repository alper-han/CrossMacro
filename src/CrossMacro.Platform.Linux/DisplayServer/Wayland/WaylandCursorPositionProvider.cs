namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class WaylandCursorPositionProvider :
    IMousePositionProvider,
    IMousePositionAvailability,
    IMousePositionChangeSource,
    IOutputTopologyProvider,
    IAsyncDisposable
{
    private readonly Lock _positionLock = new();
    private readonly Lock _connectionLock = new();
    private readonly CancellationTokenSource _eventLoopCancellation = new();
    private WaylandCursorPositionConnection? _connection;
    private readonly Task _eventLoopTask;
    private bool _hasPosition;
    private int _currentX;
    private int _currentY;
    private int _supported = 1;
    private int _disposed;

    private WaylandCursorPositionProvider(CancellationToken cancellationToken)
    {
        _connection = WaylandCursorPositionConnection.Connect(OnPositionChanged, cancellationToken);
        _eventLoopTask = Task.Run(EventLoopAsync, CancellationToken.None);
    }

    public string ProviderName => "Wayland ext-image-copy cursor";
    public bool IsSupported => Volatile.Read(ref _supported) is 1 && Volatile.Read(ref _disposed) is 0;
    public bool SupportsAbsolutePosition => IsSupported;
    public bool IsPositionAvailable
    {
        get
        {
            if (!IsSupported)
            {
                return false;
            }

            lock (_positionLock)
            {
                // The protocol object can be created successfully even when
                // the compositor does not expose a live cursor image (for
                // example, a software cursor on Sway). The static capability
                // remains true, while runtime availability stays false until
                // a real position has arrived.
                return _hasPosition;
            }
        }
    }
    public event EventHandler<MousePositionChangedEventArgs>? PositionChanged;

    public static WaylandCursorPositionProvider? TryCreate(CancellationToken cancellationToken = default)
    {
        try
        {
            return CreateOrThrow(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[WaylandCursorPositionProvider] Native cursor protocol is unavailable");
            return null;
        }
    }

    internal static WaylandCursorPositionProvider CreateOrThrow(CancellationToken cancellationToken) =>
        new(cancellationToken);

    public Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        lock (_positionLock)
        {
            return Task.FromResult<(int X, int Y)?>(_hasPosition ? (_currentX, _currentY) : null);
        }
    }

    public Task<(int Width, int Height)?> GetScreenResolutionAsync()
    {
        var bounds = ReadDesktopBounds();
        return Task.FromResult<(int Width, int Height)?>(bounds is not null
            ? (bounds.Value.Width, bounds.Value.Height)
            : null);
    }

    public Task<ScreenRect?> GetDesktopBoundsAsync() => Task.FromResult(ReadDesktopBounds());

    Task<IReadOnlyList<ScreenRect>> IOutputTopologyProvider.GetOutputBoundsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_connectionLock)
        {
            IReadOnlyList<ScreenRect> bounds = _connection?.OutputBounds ?? [];
            return Task.FromResult(bounds);
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
        {
            return;
        }

        await _eventLoopCancellation.CancelAsync().ConfigureAwait(false);
        await _eventLoopTask.ConfigureAwait(false);
        DisposeConnection(DetachConnection());
        _eventLoopCancellation.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task EventLoopAsync()
    {
        var cancellationToken = _eventLoopCancellation.Token;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var connection = GetConnection()
                    ?? throw new IOException("Wayland cursor connection is unavailable.");
                connection.Dispatch(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Volatile.Write(ref _supported, 0);
                ClearPosition();
                Log.Warning(ex, "[WaylandCursorPositionProvider] Cursor connection interrupted; reconnecting");
                if (!await ReconnectAsync(cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }
        }
    }

    private async Task<bool> ReconnectAsync(CancellationToken cancellationToken)
    {
        DisposeConnection(DetachConnection());
        var retryDelay = TimeSpan.FromMilliseconds(100);
        var attempts = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            attempts++;
            try
            {
                var replacement = WaylandCursorPositionConnection.Connect(OnPositionChanged, cancellationToken);
                lock (_connectionLock)
                {
                    if (Volatile.Read(ref _disposed) is not 0)
                    {
                        replacement.Dispose();
                        return false;
                    }

                    _connection = replacement;
                }

                Volatile.Write(ref _supported, 1);
                Log.Information(
                    "[WaylandCursorPositionProvider] Cursor connection restored after {Attempts} attempt(s)",
                    attempts);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                if (attempts is 1 || (attempts & (attempts - 1)) is 0)
                {
                    Log.Debug(
                        ex,
                        "[WaylandCursorPositionProvider] Cursor reconnect attempt {Attempt} failed",
                        attempts);
                }
            }

            try
            {
                await Task.Delay(retryDelay, TimeProvider.System, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            retryDelay = TimeSpan.FromMilliseconds(Math.Min(retryDelay.TotalMilliseconds * 2, 5000));
        }

        return false;
    }

    private ScreenRect? ReadDesktopBounds()
    {
        lock (_connectionLock)
        {
            return _connection?.DesktopBounds;
        }
    }

    private WaylandCursorPositionConnection? GetConnection()
    {
        lock (_connectionLock)
        {
            return _connection;
        }
    }

    private WaylandCursorPositionConnection? DetachConnection()
    {
        lock (_connectionLock)
        {
            var connection = _connection;
            _connection = null;
            return connection;
        }
    }

    private static void DisposeConnection(WaylandCursorPositionConnection? connection)
    {
        if (connection is null)
        {
            return;
        }

        try
        {
            connection.Dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[WaylandCursorPositionProvider] Failed to dispose cursor connection");
        }
    }

    private void ClearPosition()
    {
        lock (_positionLock)
        {
            _hasPosition = false;
        }
    }

    private void OnPositionChanged(int x, int y)
    {
        bool isDiscontinuity;
        lock (_positionLock)
        {
            if (_hasPosition && _currentX == x && _currentY == y)
            {
                return;
            }

            isDiscontinuity = !_hasPosition;
            _currentX = x;
            _currentY = y;
            _hasPosition = true;
        }

        PositionChanged?.Invoke(this, new MousePositionChangedEventArgs(x, y, isDiscontinuity));
    }
}
