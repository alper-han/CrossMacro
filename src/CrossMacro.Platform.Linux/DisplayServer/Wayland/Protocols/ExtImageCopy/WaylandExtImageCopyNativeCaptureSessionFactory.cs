namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Protocols.ExtImageCopy;

public sealed class WaylandExtImageCopyNativeCaptureSessionFactory : IExtImageCopyNativeCaptureSessionFactory, IDisposable
{
    private readonly Lock _lock = new();
    private WaylandWlrConnection? _connection;
    private int _connectedGeneration;
    private bool _disposed;

    public Task<WaylandCaptureResult> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        return CaptureFrameAsyncCoreAsync(region, options);
    }

    private async Task<WaylandCaptureResult> CaptureFrameAsyncCoreAsync(ScreenRect? region, ScreenReadOptions options)
    {
        if (options.CancellationToken.IsCancellationRequested)
        {
            return WaylandCaptureResult.Failure(ScreenReadErrorKind.Canceled, "ext-image-copy-capture-v1 capture was canceled before it started.");
        }

        try
        {
            // Task.Run keeps the blocking native capture off the caller's SynchronizationContext.
            return await Task.Run(() => CaptureFrameCore(region, options), options.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return WaylandCaptureResult.Failure(ScreenReadErrorKind.Canceled, "ext-image-copy-capture-v1 capture was canceled.");
        }
        catch (TimeoutException ex)
        {
            return WaylandCaptureResult.Failure(ScreenReadErrorKind.CaptureTimeout, ex.Message);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException)
        {
            return WaylandCaptureResult.Failure(ScreenReadErrorKind.CaptureFailed, ex.Message);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _connection?.Dispose();
            _connection = null;
        }
    }

    private WaylandCaptureResult CaptureFrameCore(ScreenRect? region, ScreenReadOptions options)
    {
        options.CancellationToken.ThrowIfCancellationRequested();

        try
        {
            lock (_lock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                options.CancellationToken.ThrowIfCancellationRequested();

                var connection = GetCurrentConnection(options);
                if (connection.Registry.Shm == IntPtr.Zero ||
                    connection.Registry.ExtOutputSourceManager == IntPtr.Zero ||
                    connection.Registry.ExtCopyManager == IntPtr.Zero)
                {
                    DisposeConnectionUnsafe();
                    return WaylandCaptureResult.Failure(ScreenReadErrorKind.BackendUnavailable, "ext-image-copy required Wayland globals are unavailable.");
                }

                return WaylandCaptureResult.Success(connection.CaptureExtImageCopy(region, options));
            }
        }
        catch (OperationCanceledException)
        {
            DisposeConnection();
            throw;
        }
        catch (TimeoutException)
        {
            DisposeConnection();
            throw;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            DisposeConnection();
            throw;
        }
    }

    private void DisposeConnection()
    {
        lock (_lock)
        {
            DisposeConnectionUnsafe();
        }
    }

    private WaylandWlrConnection GetCurrentConnection(ScreenReadOptions options)
    {
        if (_connection is { } existing)
        {
            // Reads the registry events that arrived since the last capture, so a withdrawn output or
            // manager is noticed before it is used.
            existing.DisplayRoundtrip(new WaylandCaptureCancellation(options));
            if (existing.Registry.Generation == _connectedGeneration)
            {
                return existing;
            }

            DisposeConnectionUnsafe();
        }

        var connection = WaylandWlrConnection.Connect(options);
        _connectedGeneration = connection.Registry.Generation;
        _connection = connection;
        return connection;
    }

    private void DisposeConnectionUnsafe()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
