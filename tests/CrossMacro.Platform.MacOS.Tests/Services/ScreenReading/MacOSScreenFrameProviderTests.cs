using CrossMacro.Platform.MacOS.Services.ScreenReading;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.Services.ScreenReading;

public sealed class MacOSScreenFrameProviderTests
{
    [Fact]
    public void ProviderName_IsExpected()
    {
        using var provider = new MacOSScreenFrameProvider(new RecordingCaptureBackend(), new RecordingPermission(), () => true);

        Assert.Equal("macOS CoreGraphics", provider.ProviderName);
    }

    [Fact]
    public void IsSupported_UsesInjectedPlatformProbe()
    {
        using var supported = new MacOSScreenFrameProvider(new RecordingCaptureBackend(), new RecordingPermission(), () => true);
        using var unsupported = new MacOSScreenFrameProvider(new RecordingCaptureBackend(), new RecordingPermission(), () => false);

        Assert.True(supported.IsSupported);
        Assert.False(unsupported.IsSupported);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenPlatformUnsupported_ReturnsUnsupportedWithoutPermissionOrCapture()
    {
        var backend = new RecordingCaptureBackend();
        var permission = new RecordingPermission();
        using var provider = new MacOSScreenFrameProvider(backend, permission, () => false);

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Unsupported, result.ErrorKind);
        Assert.Equal(0, permission.PreflightCalls);
        Assert.Equal(0, backend.CaptureCalls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenCanceledBeforeCapture_ReturnsCanceledWithoutPermissionOrCapture()
    {
        var backend = new RecordingCaptureBackend();
        var permission = new RecordingPermission();
        using var provider = new MacOSScreenFrameProvider(backend, permission, () => true);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), new ScreenReadOptions(cancellationToken: cts.Token));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(0, permission.PreflightCalls);
        Assert.Equal(0, backend.CaptureCalls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenPermissionDenied_ReturnsPermissionDeniedAfterSingleRequest()
    {
        var backend = new RecordingCaptureBackend();
        var permission = new RecordingPermission { PreflightResults = [false, false], IsRequestAvailable = true };
        using var provider = new MacOSScreenFrameProvider(backend, permission, () => true);

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, result.ErrorKind);
        Assert.Contains("Screen Recording", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(2, permission.PreflightCalls);
        Assert.Equal(1, permission.RequestCalls);
        Assert.Equal(0, backend.CaptureCalls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenPermissionGrantedAfterRequest_Captures()
    {
        var backend = new RecordingCaptureBackend();
        var permission = new RecordingPermission { PreflightResults = [false, true], IsRequestAvailable = true };
        using var provider = new MacOSScreenFrameProvider(backend, permission, () => true);

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(1, permission.RequestCalls);
        Assert.Equal(1, backend.CaptureCalls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenPreflightUnavailable_CapturesWithoutPrompting()
    {
        var backend = new RecordingCaptureBackend();
        var permission = new RecordingPermission { IsPreflightAvailable = false, IsRequestAvailable = true };
        using var provider = new MacOSScreenFrameProvider(backend, permission, () => true);

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(0, permission.PreflightCalls);
        Assert.Equal(0, permission.RequestCalls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenRegionIsNull_CapturesVirtualScreenBounds()
    {
        var backend = new RecordingCaptureBackend
        {
            VirtualScreenBounds = new ScreenRect(-10, -20, 30, 40)
        };
        using var provider = new MacOSScreenFrameProvider(backend, new RecordingPermission(), () => true);

        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new ScreenRect(-10, -20, 30, 40), backend.LastRegion);
        Assert.Equal(new ScreenRect(-10, -20, 30, 40), result.Value!.LogicalBounds);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenRegionOutsideVirtualScreen_ReturnsOutOfBoundsWithoutCapturing()
    {
        var backend = new RecordingCaptureBackend { VirtualScreenBounds = new ScreenRect(0, 0, 10, 10) };
        var permission = new RecordingPermission { PreflightResults = [false] };
        using var provider = new MacOSScreenFrameProvider(backend, permission, () => true);

        var result = await provider.CaptureFrameAsync(new ScreenRect(-1, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.OutOfBounds, result.ErrorKind);
        Assert.Equal(0, permission.PreflightCalls);
        Assert.Equal(0, backend.CaptureCalls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenBackendUnavailable_ReturnsBackendUnavailable()
    {
        var backend = new RecordingCaptureBackend { CaptureException = new BackendUnavailableException("no displays") };
        using var provider = new MacOSScreenFrameProvider(backend, new RecordingPermission(), () => true);

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
    }

    [Theory]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(ArithmeticException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task CaptureFrameAsync_WhenBackendThrowsKnownCaptureException_ReturnsCaptureFailed(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "capture failed")!;
        var backend = new RecordingCaptureBackend { CaptureException = exception };
        using var provider = new MacOSScreenFrameProvider(backend, new RecordingPermission(), () => true);

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Contains("capture failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingPermission : IMacOSScreenCapturePermission
    {
        private int _preflightIndex;

        public bool IsPreflightAvailable { get; init; } = true;

        public bool IsRequestAvailable { get; init; }

        public bool[] PreflightResults { get; init; } = [true];

        public int PreflightCalls { get; private set; }

        public int RequestCalls { get; private set; }

        public bool Preflight()
        {
            PreflightCalls++;
            var index = Math.Min(_preflightIndex, PreflightResults.Length - 1);
            _preflightIndex++;
            return PreflightResults[index];
        }

        public bool Request()
        {
            RequestCalls++;
            return true;
        }
    }

    private sealed class RecordingCaptureBackend : IMacOSScreenCaptureBackend
    {
        public ScreenRect VirtualScreenBounds { get; init; } = new(0, 0, 100, 100);

        public ScreenRect? LastRegion { get; private set; }

        public int CaptureCalls { get; private set; }

        public Exception? CaptureException { get; init; }

        public ScreenRect GetVirtualScreenBounds() => VirtualScreenBounds;

        public MacOSScreenCaptureFrame Capture(ScreenRect region, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CaptureCalls++;
            LastRegion = region;

            if (CaptureException is { } exception)
            {
                throw exception;
            }

            var stride = checked(region.Width * ScreenFrame.GetBytesPerPixel(ScreenPixelFormat.Bgra8888));
            var pixels = new byte[checked(stride * region.Height)];
            for (var offset = 0; offset < pixels.Length; offset += 4)
            {
                pixels[offset] = 0x11;
                pixels[offset + 1] = 0x22;
                pixels[offset + 2] = 0x33;
            }

            return new MacOSScreenCaptureFrame(region, stride, ScreenPixelFormat.Bgra8888, pixels);
        }
    }
}
