namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class WaylandExtImageCopyNativeCaptureSessionFactory : IExtImageCopyNativeCaptureSessionFactory, IDisposable
{
    private readonly Lock _lock = new();
    private WaylandWlrConnection? _connection;
    private bool _disposed;

    public Task<ExtImageCopyCaptureResult> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        return CaptureFrameAsyncCoreAsync(region, options);
    }

    private async Task<ExtImageCopyCaptureResult> CaptureFrameAsyncCoreAsync(ScreenRect? region, ScreenReadOptions options)
    {
        if (options.CancellationToken.IsCancellationRequested)
        {
            return ExtImageCopyCaptureResult.Failure(ScreenReadErrorKind.Canceled, "ext-image-copy-capture-v1 capture was canceled before it started.");
        }

        try
        {
            // Task.Run keeps the blocking native capture off the caller's SynchronizationContext.
            return await Task.Run(() => CaptureFrameCore(region, options), options.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ExtImageCopyCaptureResult.Failure(ScreenReadErrorKind.Canceled, "ext-image-copy-capture-v1 capture was canceled.");
        }
        catch (TimeoutException ex)
        {
            return ExtImageCopyCaptureResult.Failure(ScreenReadErrorKind.CaptureTimeout, ex.Message);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException)
        {
            return ExtImageCopyCaptureResult.Failure(ScreenReadErrorKind.CaptureFailed, ex.Message);
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

    private ExtImageCopyCaptureResult CaptureFrameCore(ScreenRect? region, ScreenReadOptions options)
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
