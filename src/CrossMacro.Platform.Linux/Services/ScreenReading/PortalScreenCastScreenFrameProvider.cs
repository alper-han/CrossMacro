
namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public sealed class PortalScreenCastScreenFrameProvider(IPortalScreenCastCapture capture, PortalScreenCastSupportResult support) : IScreenFrameProvider
{
    private readonly IPortalScreenCastCapture _capture = capture ?? throw new ArgumentNullException(nameof(capture));
    private readonly PortalScreenCastSupportResult _support = support;
    private bool _disposed;

    public PortalScreenCastScreenFrameProvider(IPortalScreenCastCapture capture)
        : this(capture, capture?.ProbeSupport() ?? throw new ArgumentNullException(nameof(capture))) { /* Empty */ }

    public string ProviderName => "XDG Desktop Portal ScreenCast";

    public bool IsSupported => _support.IsSupported;

    public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_support.IsSupported)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                _support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                _support.ErrorMessage ?? "XDG Desktop Portal ScreenCast is unavailable.");
        }

        if (options.CancellationToken.IsCancellationRequested)
        {
            return LinuxScreenFrameProviderResults.CanceledBeforeStart("XDG Desktop Portal ScreenCast capture was canceled before it started.");
        }

        PortalScreenCastCaptureResult captureResult;
        try
        {
            captureResult = await _capture.CaptureSupportedAsync(region, options).ConfigureAwait(false);
        }
        catch (Exception ex) when (LinuxScreenFrameProviderResults.IsKnownCaptureException(ex))
        {
            return LinuxScreenFrameProviderResults.FromKnownCaptureException(ex, "XDG Desktop Portal ScreenCast capture was canceled.");
        }

        if (!captureResult.IsSuccess)
        {
            return LinuxScreenFrameProviderResults.FromCaptureFailure(
                captureResult.ErrorKind,
                captureResult.ErrorMessage,
                "XDG Desktop Portal ScreenCast capture failed.");
        }

        var frame = captureResult.Frame;
        if (frame is null)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.CaptureFailed,
                "Successful XDG Desktop Portal capture did not include a frame.");
        }

        if (region is null || region.Value == frame.LogicalBounds)
        {
            return LinuxScreenFrameProviderResults.CreateSharedFrame(frame.LogicalBounds, frame.Stride, frame.PixelFormat, frame.Pixels, frame, frame.ValidPixelMask);
        }

        try
        {
            return CopyRegionForResult(region.Value, frame);
        }
        finally
        {
            frame.Dispose();
        }
    }

    private static ScreenReadResult<ScreenFrame> CopyRegionForResult(ScreenRect region, PortalPipeWireFrame frame)
    {
        if (!frame.LogicalBounds.Contains(region))
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.OutOfBounds,
                $"Requested region {region} is outside XDG Desktop Portal frame bounds {frame.LogicalBounds}.");
        }

        return ScreenReadResultFactory.Success<ScreenFrame>(LinuxScreenFrameProviderResults.CopyRegion(
            frame.LogicalBounds,
            frame.Stride,
            frame.PixelFormat,
            frame.Pixels,
            region,
            frame.ValidPixelMask));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _capture.Dispose();
    }
}
