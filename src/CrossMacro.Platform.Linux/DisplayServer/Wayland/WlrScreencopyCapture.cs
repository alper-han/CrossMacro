using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WlrScreencopyCapture : IWlrScreencopyCapture
{
    public WlrScreencopySupportResult ProbeSupport()
    {
        try
        {
            using var connection = WaylandWlrConnection.Connect();
            if (connection.Registry.Shm == IntPtr.Zero)
            {
                return WlrScreencopySupportResult.Unsupported("Wayland registry did not expose wl_shm.");
            }

            if (connection.Registry.WlrScreencopyManager == IntPtr.Zero)
            {
                return WlrScreencopySupportResult.Unsupported("Wayland registry did not expose zwlr_screencopy_manager_v1.");
            }

            if (connection.Registry.Outputs.Count == 0)
            {
                return WlrScreencopySupportResult.Unsupported("Wayland registry did not expose any wl_output globals.");
            }

            return WlrScreencopySupportResult.Supported();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
            return WlrScreencopySupportResult.Failure(ScreenReadErrorKind.BackendUnavailable, ex.Message);
        }
    }

    public async Task<WlrScreencopyCaptureResult> CaptureRegionAsync(ScreenRect? region, ScreenReadOptions options)
    {
        if (options.CancellationToken.IsCancellationRequested)
        {
            return WlrScreencopyCaptureResult.Failure(ScreenReadErrorKind.Canceled, "wlr-screencopy capture was canceled before it started.");
        }

        try
        {
            return await Task.FromResult(CaptureRegion(region, options)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return WlrScreencopyCaptureResult.Failure(ScreenReadErrorKind.Canceled, "wlr-screencopy capture was canceled.");
        }
        catch (TimeoutException)
        {
            return WlrScreencopyCaptureResult.Failure(ScreenReadErrorKind.CaptureTimeout, "wlr-screencopy capture timed out.");
        }
    }

    public void Dispose()
    {
    }

    private static WlrScreencopyCaptureResult CaptureRegion(ScreenRect? region, ScreenReadOptions options)
    {
        try
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            using var connection = WaylandWlrConnection.Connect();
            options.CancellationToken.ThrowIfCancellationRequested();
            if (connection.Registry.Shm == IntPtr.Zero || connection.Registry.WlrScreencopyManager == IntPtr.Zero)
            {
                return WlrScreencopyCaptureResult.Failure(ScreenReadErrorKind.BackendUnavailable, "wlr-screencopy required Wayland globals are unavailable.");
            }

            return WlrScreencopyCaptureResult.Success(connection.Capture(region));
        }
        catch (OperationCanceledException)
        {
            return WlrScreencopyCaptureResult.Failure(ScreenReadErrorKind.Canceled, "wlr-screencopy capture was canceled.");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return WlrScreencopyCaptureResult.Failure(ScreenReadErrorKind.OutOfBounds, ex.Message);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or IOException)
        {
            return WlrScreencopyCaptureResult.Failure(ScreenReadErrorKind.CaptureFailed, ex.Message);
        }
    }
}
