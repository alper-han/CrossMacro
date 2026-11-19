
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Protocols.WlrScreencopy;

internal sealed class WlrScreencopyCapture : IWlrScreencopyCapture
{
    private readonly Func<WaylandSupportResult> _probeSupport;

    public WlrScreencopyCapture()
        : this(ProbeSupportCore) { /* Empty */ }

    internal WlrScreencopyCapture(Func<WaylandSupportResult> probeSupport)
    {
        _probeSupport = probeSupport ?? throw new ArgumentNullException(nameof(probeSupport));
    }

    public WaylandSupportResult ProbeSupport()
    {
        try
        {
            return _probeSupport();
        }
        catch (IOException ex)
        {
            return WaylandSupportResult.Failure(ScreenReadErrorKind.BackendUnavailable, ex.Message);
        }
    }

    private static WaylandSupportResult ProbeSupportCore()
    {
        try
        {
            using var connection = WaylandWlrConnection.Connect(new ScreenReadOptions(timeout: WaylandTimeouts.Operation));
            if (connection.Registry.Shm == IntPtr.Zero)
            {
                return WaylandSupportResult.Unsupported("Wayland registry did not expose wl_shm.");
            }

            if (connection.Registry.WlrScreencopyManager == IntPtr.Zero)
            {
                return WaylandSupportResult.Unsupported("Wayland registry did not expose zwlr_screencopy_manager_v1.");
            }

            if (connection.Registry.Outputs.Count is 0)
            {
                return WaylandSupportResult.Unsupported("Wayland registry did not expose any wl_output globals.");
            }

            return WaylandSupportResult.Supported();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or TimeoutException)
        {
            return WaylandSupportResult.Failure(ScreenReadErrorKind.BackendUnavailable, ex.Message);
        }
    }

    public async Task<WaylandCaptureResult> CaptureRegionAsync(ScreenRect? region, ScreenReadOptions options)
    {
        if (options.CancellationToken.IsCancellationRequested)
        {
            return WaylandCaptureResult.Failure(ScreenReadErrorKind.Canceled, "wlr-screencopy capture was canceled before it started.");
        }

        try
        {
            // Task.Run keeps the blocking native capture off the caller's SynchronizationContext.
            return await Task.Run(() => CaptureRegion(region, options), options.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return WaylandCaptureResult.Failure(ScreenReadErrorKind.Canceled, "wlr-screencopy capture was canceled.");
        }
        catch (TimeoutException)
        {
            return WaylandCaptureResult.Failure(ScreenReadErrorKind.CaptureTimeout, "wlr-screencopy capture timed out.");
        }
    }

    public void Dispose() { /* Empty */ }

    private static WaylandCaptureResult CaptureRegion(ScreenRect? region, ScreenReadOptions options)
    {
        try
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            using var connection = WaylandWlrConnection.Connect(options);
            options.CancellationToken.ThrowIfCancellationRequested();
            if (connection.Registry.Shm == IntPtr.Zero || connection.Registry.WlrScreencopyManager == IntPtr.Zero)
            {
                return WaylandCaptureResult.Failure(ScreenReadErrorKind.BackendUnavailable, "wlr-screencopy required Wayland globals are unavailable.");
            }

            return WaylandCaptureResult.Success(connection.Capture(region, options));
        }
        catch (OperationCanceledException)
        {
            return WaylandCaptureResult.Failure(ScreenReadErrorKind.Canceled, "wlr-screencopy capture was canceled.");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return WaylandCaptureResult.Failure(ScreenReadErrorKind.OutOfBounds, ex.Message);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or IOException)
        {
            return WaylandCaptureResult.Failure(ScreenReadErrorKind.CaptureFailed, ex.Message);
        }
    }
}
