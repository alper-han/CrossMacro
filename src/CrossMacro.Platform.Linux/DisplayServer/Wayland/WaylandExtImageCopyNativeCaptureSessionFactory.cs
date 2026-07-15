namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class WaylandExtImageCopyNativeCaptureSessionFactory : IExtImageCopyNativeCaptureSessionFactory
{
    private readonly Lock _lock = new();
    private WaylandWlrConnection? _connection;
    private bool _disposed;

    public Task<ExtImageCopyCaptureResult> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        return Task.Run(() => CaptureFrame(region, options));
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

    private ExtImageCopyCaptureResult CaptureFrame(ScreenRect? region, ScreenReadOptions options)
    {
        options.CancellationToken.ThrowIfCancellationRequested();

        try
        {
            lock (_lock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                options.CancellationToken.ThrowIfCancellationRequested();

                _connection ??= WaylandWlrConnection.Connect(options);
                if (_connection.Registry.Shm == IntPtr.Zero ||
                    _connection.Registry.ExtOutputSourceManager == IntPtr.Zero ||
                    _connection.Registry.ExtCopyManager == IntPtr.Zero)
                {
                    DisposeConnectionUnsafe();
                    return ExtImageCopyCaptureResult.Failure(ScreenReadErrorKind.BackendUnavailable, "ext-image-copy required Wayland globals are unavailable.");
                }

                return ExtImageCopyCaptureResult.Success(_connection.CaptureExtImageCopy(region, options));
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

    private void DisposeConnectionUnsafe()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
