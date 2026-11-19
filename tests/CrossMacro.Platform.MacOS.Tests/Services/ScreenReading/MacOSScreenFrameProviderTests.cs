using CrossMacro.Platform.MacOS.Services.ScreenReading;

namespace CrossMacro.Platform.MacOS.Tests.Services.ScreenReading;

public sealed class MacOSScreenFrameProviderTests
{
    [Fact]
    public void ProviderName_IsExpected()
    {
        using var provider = new MacOSScreenFrameProvider(new RecordingCaptureBackend(), new RecordingPermissionNative());

        Assert.Equal("macOS CoreGraphics", provider.ProviderName);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenCanceledBeforeCapture_ReturnsCanceledWithoutPermissionOrCapture()
    {
        var backend = new RecordingCaptureBackend();
        var permission = new RecordingPermissionNative();
        using var provider = new MacOSScreenFrameProvider(backend, permission);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), new ScreenReadOptions(cancellationToken: cts.Token));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Empty(permission.Calls);
        Assert.Equal(0, backend.CaptureCalls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenPermissionDenied_ReturnsPermissionDeniedAfterSingleRequest()
    {
        var backend = new RecordingCaptureBackend();
        var permission = new RecordingPermissionNative { ScreenCapturePreflightResults = [false, false] };
        using var provider = new MacOSScreenFrameProvider(backend, permission);

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, result.ErrorKind);
        Assert.Contains("Screen Recording", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(["PreflightScreenCapture", "RequestScreenCapture", "PreflightScreenCapture"], permission.Calls);
        Assert.Equal(0, backend.CaptureCalls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenPermissionStaysDenied_RequestsOnlyOncePerProviderAndNeverOpensSettings()
    {
        var backend = new RecordingCaptureBackend();
        var permission = new RecordingPermissionNative { ScreenCapturePreflightResults = [false] };
        using var provider = new MacOSScreenFrameProvider(backend, permission);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);
            Assert.Equal(ScreenReadErrorKind.PermissionDenied, result.ErrorKind);
        }

        Assert.Equal(1, permission.Count("RequestScreenCapture"));
        Assert.DoesNotContain(permission.Calls, call => call.StartsWith("OpenPrivacySettings", StringComparison.Ordinal));
        Assert.Equal(0, backend.CaptureCalls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenPermissionGrantedAfterRequest_Captures()
    {
        var backend = new RecordingCaptureBackend();
        var permission = new RecordingPermissionNative { ScreenCapturePreflightResults = [false, true] };
        using var provider = new MacOSScreenFrameProvider(backend, permission);

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(1, permission.Count("RequestScreenCapture"));
        Assert.Equal(1, backend.CaptureCalls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenPermissionAlreadyGranted_CapturesWithoutPrompting()
    {
        var backend = new RecordingCaptureBackend();
        var permission = new RecordingPermissionNative { ScreenCapturePreflightResults = [true] };
        using var provider = new MacOSScreenFrameProvider(backend, permission);

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(["PreflightScreenCapture"], permission.Calls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenRegionIsNull_CapturesVirtualScreenBounds()
    {
        var backend = new RecordingCaptureBackend
        {
            VirtualScreenBounds = new ScreenRect(-10, -20, 30, 40),
        };
        using var provider = new MacOSScreenFrameProvider(backend, new RecordingPermissionNative { ScreenCapturePreflightResults = [true] });

        var result = await provider.CaptureFrameAsync(region: null, ScreenReadOptions.Default);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new ScreenRect(-10, -20, 30, 40), backend.LastRegion);
        Assert.Equal(new ScreenRect(-10, -20, 30, 40), result.Value!.LogicalBounds);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenRegionOutsideVirtualScreen_ReturnsOutOfBoundsWithoutCapturing()
    {
        var backend = new RecordingCaptureBackend { VirtualScreenBounds = new ScreenRect(0, 0, 10, 10) };
        var permission = new RecordingPermissionNative();
        using var provider = new MacOSScreenFrameProvider(backend, permission);

        var result = await provider.CaptureFrameAsync(new ScreenRect(-1, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.OutOfBounds, result.ErrorKind);
        Assert.Empty(permission.Calls);
        Assert.Equal(0, backend.CaptureCalls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenBackendUnavailable_ReturnsBackendUnavailable()
    {
        var backend = new RecordingCaptureBackend { CaptureException = new BackendUnavailableException("no displays") };
        using var provider = new MacOSScreenFrameProvider(backend, new RecordingPermissionNative { ScreenCapturePreflightResults = [true] });

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
    }

    public static TheoryData<Exception> KnownCaptureExceptions => new()
    {
        CreateArgumentException("capture failed"),
        new ArithmeticException("capture failed"),
        new InvalidOperationException("capture failed"),
    };

    [Theory]
    [MemberData(nameof(KnownCaptureExceptions))]
    public async Task CaptureFrameAsync_WhenBackendThrowsKnownCaptureException_ReturnsCaptureFailed(Exception exception)
    {
        var backend = new RecordingCaptureBackend { CaptureException = exception };
        using var provider = new MacOSScreenFrameProvider(backend, new RecordingPermissionNative { ScreenCapturePreflightResults = [true] });

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Contains("capture failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static ArgumentException CreateArgumentException(string message) =>
        new(message, nameof(message));

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
