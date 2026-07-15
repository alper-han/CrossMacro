using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.Services;
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class ExtImageCopyCapture : IExtImageCopyCapture
{
    private readonly IExtImageCopySupportProbe _supportProbe;
    private readonly IExtImageCopyNativeCaptureSessionFactory _sessionFactory;
    private bool _disposed;

    public ExtImageCopyCapture()
        : this(WaylandExtImageCopySupportProbe.Instance, new WaylandExtImageCopyNativeCaptureSessionFactory())
    {
    }

    public ExtImageCopyCapture(
        IExtImageCopySupportProbe supportProbe,
        IExtImageCopyNativeCaptureSessionFactory sessionFactory)
    {
        _supportProbe = supportProbe ?? throw new ArgumentNullException(nameof(supportProbe));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    }

    public ExtImageCopySupportResult ProbeSupport() => _supportProbe.ProbeSupport();

    public async Task<ExtImageCopyCaptureResult> CaptureAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var support = ProbeSupport();
        if (!support.IsSupported)
        {
            return ExtImageCopyCaptureResult.Failure(
                support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                support.ErrorMessage ?? "ext-image-copy-capture-v1 is unavailable.");
        }

        return await CaptureSupportedAsync(region, options).ConfigureAwait(false);
    }

    public async Task<ExtImageCopyCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (options.CancellationToken.IsCancellationRequested)
        {
            return ExtImageCopyCaptureResult.Failure(ScreenReadErrorKind.Canceled, "ext-image-copy-capture-v1 capture was canceled before it started.");
        }

        try
        {
            return await _sessionFactory.CaptureFrameAsync(region, options).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ExtImageCopyCaptureResult.Failure(ScreenReadErrorKind.Canceled, "ext-image-copy-capture-v1 capture was canceled.");
        }
        catch (TimeoutException ex)
        {
            return ExtImageCopyCaptureResult.Failure(ScreenReadErrorKind.CaptureTimeout, ex.Message);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return ExtImageCopyCaptureResult.Failure(ScreenReadErrorKind.CaptureFailed, ex.Message);
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
