namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class LinuxScreenFrameProviderResultsTests
{
    [Fact]
    public void SupportsRequest_AllKnownBackendsSupportFullFrames()
    {
        foreach (var backend in Enum.GetValues<LinuxScreenReaderBackend>())
        {
            Assert.True(LinuxScreenFrameCaptureModes.SupportsRequest(backend, isFullFrameRequest: true));
            Assert.True(LinuxScreenFrameCaptureModes.SupportsRequest(backend, isFullFrameRequest: false));
        }
    }

    [Fact]
    public void SupportsRequest_UnknownBackendThrowsOnlyForFullFrameRequests()
    {
        var unknown = (LinuxScreenReaderBackend)999;

        Assert.True(LinuxScreenFrameCaptureModes.SupportsRequest(unknown, isFullFrameRequest: false));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => LinuxScreenFrameCaptureModes.SupportsRequest(unknown, isFullFrameRequest: true));
    }

    [Fact]
    public void IsKnownCaptureException_RecognizesExpectedCaptureFailures()
    {
        Assert.True(LinuxScreenFrameProviderResults.IsKnownCaptureException(new OperationCanceledException()));
        Assert.True(LinuxScreenFrameProviderResults.IsKnownCaptureException(new TimeoutException()));
        Assert.True(LinuxScreenFrameProviderResults.IsKnownCaptureException(new InvalidOperationException()));
        Assert.True(LinuxScreenFrameProviderResults.IsKnownCaptureException(new IOException()));
        Assert.True(LinuxScreenFrameProviderResults.IsKnownCaptureException(new UnauthorizedAccessException()));
        Assert.False(LinuxScreenFrameProviderResults.IsKnownCaptureException(new NotSupportedException("unsupported")));
    }

    [Fact]
    public void FromKnownCaptureException_MapsStableErrorKindsAndMessages()
    {
        const string canceledMessage = "capture canceled";
        AssertFailure(
            LinuxScreenFrameProviderResults.FromKnownCaptureException(new OperationCanceledException("operation"), canceledMessage),
            ScreenReadErrorKind.Canceled,
            canceledMessage);
        AssertFailure(
            LinuxScreenFrameProviderResults.FromKnownCaptureException(new TimeoutException("timed out"), canceledMessage),
            ScreenReadErrorKind.CaptureTimeout,
            "timed out");
        AssertFailure(
            LinuxScreenFrameProviderResults.FromKnownCaptureException(new InvalidOperationException("invalid"), canceledMessage),
            ScreenReadErrorKind.CaptureFailed,
            "invalid");
        AssertFailure(
            LinuxScreenFrameProviderResults.FromKnownCaptureException(new IOException("io"), canceledMessage),
            ScreenReadErrorKind.CaptureFailed,
            "io");
        AssertFailure(
            LinuxScreenFrameProviderResults.FromKnownCaptureException(new UnauthorizedAccessException("denied"), canceledMessage),
            ScreenReadErrorKind.CaptureFailed,
            "denied");
    }

    [Fact]
    public void FromKnownCaptureException_RejectsUnknownException()
    {
        _ = Assert.Throws<ArgumentException>(() => LinuxScreenFrameProviderResults.FromKnownCaptureException(
            new NotSupportedException("bad"),
            "capture canceled"));
    }

    [Fact]
    public void CanceledBeforeStart_ReturnsCanceledFailure()
    {
        AssertFailure(
            LinuxScreenFrameProviderResults.CanceledBeforeStart("not started"),
            ScreenReadErrorKind.Canceled,
            "not started");
    }

    [Fact]
    public void FromCaptureFailure_UsesFallbacksAndPreservesExplicitValues()
    {
        AssertFailure(
            LinuxScreenFrameProviderResults.FromCaptureFailure(errorKind: null, errorMessage: null, fallbackMessage: "fallback"),
            ScreenReadErrorKind.CaptureFailed,
            "fallback");
        AssertFailure(
            LinuxScreenFrameProviderResults.FromCaptureFailure(ScreenReadErrorKind.PermissionDenied, "denied", "fallback"),
            ScreenReadErrorKind.PermissionDenied,
            "denied");
    }

    [Fact]
    public void CreateSharedFrame_DisposesOwnerWhenFrameValidationFails()
    {
        var owner = new RecordingDisposable();

        var result = LinuxScreenFrameProviderResults.CreateSharedFrame(
            new ScreenRect(0, 0, 1, 1),
            stride: 1,
            ScreenPixelFormat.Rgb24,
            new byte[3],
            owner);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.True(owner.IsDisposed);
    }

    [Fact]
    public void CopyRegion_CopiesPixelsAndValidityMaskInLogicalCoordinates()
    {
        using var frame = LinuxScreenFrameProviderResults.CopyRegion(
            new ScreenRect(10, 20, 2, 2),
            sourceStride: 8,
            ScreenPixelFormat.Rgb24,
            new byte[]
            {
                0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0xAA, 0xAA,
                0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0xBB, 0xBB,
            },
            new ScreenRect(11, 20, 1, 2),
            sourceValidPixelMask: new byte[] { 1, 0, 0, 1 });

        Assert.Equal(new ScreenRect(11, 20, 1, 2), frame.LogicalBounds);
        Assert.Equal(3, frame.Stride);
        Assert.Equal(new byte[] { 0x04, 0x05, 0x06, 0x0A, 0x0B, 0x0C }, frame.Pixels.ToArray());
        Assert.Equal(new byte[] { 0, 1 }, frame.ValidPixelMask.ToArray());
    }

    [Fact]
    public async Task UnavailableProvider_ReturnsConfiguredFailureAndMetadata()
    {
        var provider = new UnavailableLinuxScreenFrameProvider(ScreenReadErrorKind.PermissionDenied, "permission denied");

        Assert.False(provider.IsSupported);
        Assert.Equal("Linux Screen Reader (Unavailable)", provider.ProviderName);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, provider.ErrorKind);
        Assert.Equal("permission denied", provider.FailureMessage);

        var result = await provider.CaptureFrameAsync(region: null, options: new ScreenReadOptions());

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, result.ErrorKind);
        Assert.Equal("permission denied", result.ErrorMessage);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UnavailableProvider_RejectsBlankFailureMessage(string failureMessage)
    {
        _ = Assert.Throws<ArgumentException>(() => new UnavailableLinuxScreenFrameProvider(
            ScreenReadErrorKind.BackendUnavailable,
            failureMessage));
    }

    private static void AssertFailure(
        ScreenReadResult<ScreenFrame> result,
        ScreenReadErrorKind expectedErrorKind,
        string expectedMessage)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedErrorKind, result.ErrorKind);
        Assert.Equal(expectedMessage, result.ErrorMessage);
        Assert.Null(result.Value);
    }

    private sealed class RecordingDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
