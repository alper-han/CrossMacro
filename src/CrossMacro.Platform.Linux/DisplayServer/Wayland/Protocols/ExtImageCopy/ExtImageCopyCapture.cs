namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Protocols.ExtImageCopy;

public sealed class ExtImageCopyCapture(
    IExtImageCopySupportProbe supportProbe,
    IExtImageCopyNativeCaptureSessionFactory sessionFactory) : IExtImageCopyCapture
{
    private readonly IExtImageCopySupportProbe _supportProbe = supportProbe ?? throw new ArgumentNullException(nameof(supportProbe));
    private readonly IExtImageCopyNativeCaptureSessionFactory _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    private bool _disposed;

    public ExtImageCopyCapture()
        : this(WaylandExtImageCopySupportProbe.Instance, new WaylandExtImageCopyNativeCaptureSessionFactory()) { /* Empty */ }

    public WaylandSupportResult ProbeSupport() => _supportProbe.ProbeSupport();

    public async Task<WaylandCaptureResult> CaptureAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var support = ProbeSupport();
        if (!support.IsSupported)
        {
            return WaylandCaptureResult.Failure(
                support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                support.ErrorMessage ?? "ext-image-copy-capture-v1 is unavailable.");
        }

        return await CaptureSupportedAsync(region, options).ConfigureAwait(false);
    }

    public async Task<WaylandCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (options.CancellationToken.IsCancellationRequested)
        {
            return WaylandCaptureResult.Failure(ScreenReadErrorKind.Canceled, "ext-image-copy-capture-v1 capture was canceled before it started.");
        }

        try
        {
            return await _sessionFactory.CaptureFrameAsync(region, options).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return WaylandCaptureResult.Failure(ScreenReadErrorKind.Canceled, "ext-image-copy-capture-v1 capture was canceled.");
        }
        catch (TimeoutException ex)
        {
            return WaylandCaptureResult.Failure(ScreenReadErrorKind.CaptureTimeout, ex.Message);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return WaylandCaptureResult.Failure(ScreenReadErrorKind.CaptureFailed, ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_sessionFactory is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
