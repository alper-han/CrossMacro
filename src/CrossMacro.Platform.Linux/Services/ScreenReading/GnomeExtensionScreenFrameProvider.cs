
namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public sealed class GnomeExtensionScreenFrameProvider : IScreenFrameProvider
{
    private readonly GnomePositionProvider _positionProvider;
    private readonly GnomeExtensionSupportResult _support;
    private bool _disposed;

    public GnomeExtensionScreenFrameProvider(GnomePositionProvider positionProvider)
    {
        ArgumentNullException.ThrowIfNull(positionProvider);
        _positionProvider = positionProvider;
        _support = ProbeSupport(positionProvider);
    }

    public GnomeExtensionScreenFrameProvider(GnomePositionProvider positionProvider, GnomeExtensionSupportResult support)
    {
        _positionProvider = positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));
        _support = support;
    }

    public string ProviderName => "GNOME Shell Extension (RAM)";

    public bool IsSupported => _support.IsSupported;

    public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_support.IsSupported)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                _support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                _support.ErrorMessage ?? "GNOME Shell extension screen reading is unavailable.");
        }

        if (options.CancellationToken.IsCancellationRequested)
        {
            return LinuxScreenFrameProviderResults.CanceledBeforeStart("GNOME Shell extension screen capture was canceled before it started.");
        }

        var resolution = await _positionProvider.GetScreenResolutionAsync().ConfigureAwait(false);
        if (resolution is null)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.CaptureFailed,
                "Failed to retrieve screen resolution from GNOME extension.");
        }

        var bounds = region ?? new ScreenRect(0, 0, resolution.Value.Width, resolution.Value.Height);

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.OutOfBounds,
                $"Invalid GNOME Shell extension capture region {bounds}.");
        }

        try
        {
            return await CaptureCoreAsync(bounds).ConfigureAwait(false);
        }
        catch (Exception ex) when (LinuxScreenFrameProviderResults.IsKnownCaptureException(ex))
        {
            return LinuxScreenFrameProviderResults.FromKnownCaptureException(ex, "GNOME Shell extension capture was canceled.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.CaptureFailed,
                ex.Message);
        }
    }

    private async Task<ScreenReadResult<ScreenFrame>> CaptureCoreAsync(ScreenRect bounds)
    {
        var captureResult = await _positionProvider.CaptureAreaAsync(bounds).ConfigureAwait(false);
        if (captureResult is null)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.CaptureFailed,
                "GNOME Shell extension capture returned no data.");
        }

        var frame = new GnomeExtensionScreenFrame(
            bounds,
            captureResult.Value.Stride,
            captureResult.Value.Format,
            captureResult.Value.Pixels,
            captureResult.Value.AlphaMode);

        return LinuxScreenFrameProviderResults.CreateSharedFrame(
            frame.LogicalBounds,
            frame.Stride,
            frame.PixelFormat,
            frame.Pixels,
            frame,
            alphaMode: frame.AlphaMode);
    }

    private static GnomeExtensionSupportResult ProbeSupport(GnomePositionProvider provider)
    {
        if (!provider.IsSupported)
        {
            return GnomeExtensionSupportResult.Failure(ScreenReadErrorKind.BackendUnavailable, "GNOME Shell is not active.");
        }

        var status = provider.CurrentExtensionStatus;
        if (status is null || status.Code is not CrossMacro.Core.Services.ExtensionStatusCode.Enabled)
        {
            return GnomeExtensionSupportResult.Failure(
                ScreenReadErrorKind.BackendUnavailable,
                $"GNOME Shell extension backend is not active (Status: {status?.Code.ToString() ?? "Unknown"}).");
        }

        return GnomeExtensionSupportResult.Supported();
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
