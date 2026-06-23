using System.ComponentModel;
using System.Runtime.InteropServices;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

public sealed class MacOSScreenFrameProvider : IScreenFrameProvider
{
    private const string PermissionMessage = "macOS Screen Recording permission is required for pixelcolor, waitcolor, and pixelsearch. Enable it in System Settings > Privacy & Security > Screen Recording, then restart CrossMacro.";

    private readonly IMacOSScreenCaptureBackend _captureBackend;
    private readonly IMacOSScreenCapturePermission _permission;
    private readonly Func<bool> _isSupportedProbe;
    private bool _disposed;

    public MacOSScreenFrameProvider()
        : this(new CoreGraphicsMacOSScreenCaptureBackend(), new CoreGraphicsScreenCapturePermission(), () => OperatingSystem.IsMacOSVersionAtLeast(10, 15))
    {
    }

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

    public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsSupported)
        {
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(
                ScreenReadErrorKind.Unsupported,
                "macOS CoreGraphics screen reading requires macOS 10.15 or newer."));
        }

        if (options.CancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(
                ScreenReadErrorKind.Canceled,
                "macOS CoreGraphics screen capture was canceled before it started."));
        }

        try
        {
            var virtualScreen = _captureBackend.GetVirtualScreenBounds();
            var captureRegion = region ?? virtualScreen;
            if (!virtualScreen.Contains(captureRegion))
            {
                return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(
                    ScreenReadErrorKind.OutOfBounds,
                    $"Requested region {captureRegion} is outside macOS virtual screen bounds {virtualScreen}."));
            }

            if (!EnsurePermission())
            {
                return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(ScreenReadErrorKind.PermissionDenied, PermissionMessage));
            }

            var captured = _captureBackend.Capture(captureRegion, options.CancellationToken);
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Success(new ScreenFrame(
                captured.LogicalBounds,
                captured.Stride,
                captured.PixelFormat,
                captured.Pixels)));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(
                ScreenReadErrorKind.Canceled,
                "macOS CoreGraphics screen capture was canceled."));
        }
        catch (BackendUnavailableException ex)
        {
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(ScreenReadErrorKind.BackendUnavailable, ex.Message));
        }
        catch (Exception ex) when (ex is ArgumentException or ArithmeticException or ExternalException or Win32Exception or InvalidOperationException)
        {
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(ScreenReadErrorKind.CaptureFailed, ex.Message));
        }
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
            _permission.Request();
        }

        return _permission.Preflight();
    }
}

internal interface IMacOSScreenCapturePermission
{
    bool IsPreflightAvailable { get; }

    bool IsRequestAvailable { get; }

    bool Preflight();

    bool Request();
}

internal sealed class CoreGraphicsScreenCapturePermission : IMacOSScreenCapturePermission
{
    public bool IsPreflightAvailable => CoreGraphics.IsCGPreflightScreenCaptureAccessAvailable();

    public bool IsRequestAvailable => CoreGraphics.IsCGRequestScreenCaptureAccessAvailable();

    public bool Preflight() => CoreGraphics.CGPreflightScreenCaptureAccess();

    public bool Request() => CoreGraphics.CGRequestScreenCaptureAccess();
}

internal sealed class CoreGraphicsScreenRecordingPermissionProbe : IMacOSScreenRecordingPermissionProbe
{
    public bool IsPreflightAvailable => CoreGraphics.IsCGPreflightScreenCaptureAccessAvailable();

    public bool IsGranted() => CoreGraphics.CGPreflightScreenCaptureAccess();
}

internal interface IMacOSScreenCaptureBackend
{
    ScreenRect GetVirtualScreenBounds();

    MacOSScreenCaptureFrame Capture(ScreenRect region, CancellationToken cancellationToken);
}

internal sealed record MacOSScreenCaptureFrame(
    ScreenRect LogicalBounds,
    int Stride,
    ScreenPixelFormat PixelFormat,
    byte[] Pixels);

internal sealed class BackendUnavailableException(string message) : InvalidOperationException(message);
