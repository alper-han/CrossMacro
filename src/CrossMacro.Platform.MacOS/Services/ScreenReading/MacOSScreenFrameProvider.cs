
namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

public sealed class MacOSScreenFrameProvider : IScreenFrameProvider
{
    private const string PermissionMessage = "macOS Screen Recording permission is required for pixelcolor, waitcolor, and pixelsearch. Enable it in System Settings > Privacy & Security > Screen Recording, then restart CrossMacro.";

    private readonly IMacOSScreenCaptureBackend _captureBackend;
    private readonly IMacOSScreenCapturePermission _permission;
    private readonly Func<bool> _isSupportedProbe;
    private bool _disposed;

    public MacOSScreenFrameProvider()
        : this(new CoreGraphicsMacOSScreenCaptureBackend(), new CoreGraphicsScreenCapturePermission(), () => OperatingSystem.IsMacOSVersionAtLeast(10, 15)) { /* Empty */ }

    internal MacOSScreenFrameProvider(
        IMacOSScreenCaptureBackend captureBackend,
        IMacOSScreenCapturePermission permission,
        Func<bool> isSupportedProbe)
    {
        _captureBackend = captureBackend ?? throw new ArgumentNullException(nameof(captureBackend));
        _permission = permission ?? throw new ArgumentNullException(nameof(permission));
        _isSupportedProbe = isSupportedProbe ?? throw new ArgumentNullException(nameof(isSupportedProbe));
    }

    public string ProviderName => "macOS CoreGraphics";

    public bool IsSupported => _isSupportedProbe();

    public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // Must stay inside try: GetVirtualScreenBounds/EnsurePermission can throw
            // BackendUnavailableException, which maps to a Failure result below.
            var earlyFailure = GetEarlyFailure(region, options, out var captureRegion);
            if (earlyFailure is not null)
            {
                return earlyFailure.Value;
            }

            var captured = _captureBackend.Capture(captureRegion, options.CancellationToken);
            ScreenFrame? frame = null;
            try
            {
                frame = new ScreenFrame(
                    captured.LogicalBounds,
                    captured.Stride,
                    captured.PixelFormat,
                    captured.Pixels,
                    validPixelMask: captured.ValidPixelMask ?? [],
                    alphaMode: ScreenAlphaMode.Opaque);
                var result = ScreenReadResultFactory.Success(frame);
                frame = null; // ownership transferred to result
                return result;
            }
            finally
            {
                frame?.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.Canceled,
                "macOS CoreGraphics screen capture was canceled.");
        }
        catch (BackendUnavailableException ex)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(ScreenReadErrorKind.BackendUnavailable, ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or ArithmeticException or ExternalException or Win32Exception or InvalidOperationException)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(ScreenReadErrorKind.CaptureFailed, ex.Message);
        }
    }

    private ScreenReadResult<ScreenFrame>? GetEarlyFailure(ScreenRect? region, ScreenReadOptions options, out ScreenRect captureRegion)
    {
        captureRegion = default;

        if (!IsSupported)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.Unsupported,
                "macOS CoreGraphics screen reading requires macOS 10.15 or newer.");
        }

        if (options.CancellationToken.IsCancellationRequested)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.Canceled,
                "macOS CoreGraphics screen capture was canceled before it started.");
        }

        var virtualScreen = _captureBackend.GetVirtualScreenBounds();
        captureRegion = region ?? virtualScreen;
        if (!virtualScreen.Contains(captureRegion))
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.OutOfBounds,
                $"Requested region {captureRegion} is outside macOS virtual screen bounds {virtualScreen}.");
        }

        if (!EnsurePermission())
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(ScreenReadErrorKind.PermissionDenied, PermissionMessage);
        }

        return null;
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private bool EnsurePermission()
    {
        if (!_permission.IsPreflightAvailable)
        {
            return true;
        }

        if (_permission.Preflight())
        {
            return true;
        }

        if (_permission.IsRequestAvailable)
        {
            _ = _permission.Request();
        }

        return _permission.Preflight();
    }
}
