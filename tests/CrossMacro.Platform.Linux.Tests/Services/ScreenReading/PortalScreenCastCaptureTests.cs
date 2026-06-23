using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class PortalScreenCastCaptureTests
{
    [Fact]
    public async Task PortalCapture_WhenSupportUnavailable_ReturnsUnavailableWithoutStartingSession()
    {
        var sessionFactory = new FakePortalScreenCastSessionFactory(
            PortalScreenCastSessionResult.Failure(ScreenReadErrorKind.CaptureFailed, "should not start"));
        var pipeWireCapture = new FakePortalPipeWireFrameCapture(
            PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, "should not capture"));
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(pipeWireCapture);
        using var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Unsupported("portal unavailable")),
            sessionFactory,
            pipeWireFactory);

        var result = await capture.CaptureAsync(ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Contains("portal unavailable", result.ErrorMessage);
        Assert.Equal(0, sessionFactory.StartCalls);
        Assert.Equal(0, pipeWireFactory.CreateCalls);
    }

    [Fact]
    public async Task PortalCaptureSupported_WhenProbeUnsupported_StartsSessionAndCapturesFrame()
    {
        var owner = new CountingDisposable();
        var session = FakePortalScreenCastSessionFactory.CreateSession(width: 2, height: 1);
        var frame = ScreenReadingFrameFixtures.PortalFrame(
            new ScreenRect(0, 0, 2, 1),
            ScreenReadingFrameFixtures.TwoPixelXrgbBytes(),
            owner);
        var sessionFactory = new FakePortalScreenCastSessionFactory(PortalScreenCastSessionResult.Success(session));
        var pipeWireCapture = new FakePortalPipeWireFrameCapture(PortalPipeWireFrameResult.Success(frame));
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(pipeWireCapture);
        using var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Unsupported("probe already handled")),
            sessionFactory,
            pipeWireFactory);

        var result = await capture.CaptureSupportedAsync(ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        using var resultFrame = Assert.IsType<PortalPipeWireFrame>(result.Frame);
        Assert.Equal(new ScreenRect(0, 0, 2, 1), resultFrame.LogicalBounds);
        Assert.Equal(1, sessionFactory.StartCalls);
        Assert.Equal(1, pipeWireFactory.CreateCalls);
        Assert.Equal(1, pipeWireCapture.CaptureCalls);
    }

    [Fact]
    public async Task PortalCapture_WhenSessionDenied_ReturnsDeniedAndSkipsPipeWire()
    {
        var sessionFactory = new FakePortalScreenCastSessionFactory(
            PortalScreenCastSessionResult.Failure(ScreenReadErrorKind.PermissionDenied, "user denied portal request"));
        var pipeWireCapture = new FakePortalPipeWireFrameCapture(
            PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, "should not capture"));
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(pipeWireCapture);
        using var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Supported()),
            sessionFactory,
            pipeWireFactory);

        var result = await capture.CaptureAsync(ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, result.ErrorKind);
        Assert.Contains("user denied", result.ErrorMessage);
        Assert.Equal(1, sessionFactory.StartCalls);
        Assert.Equal(0, pipeWireFactory.CreateCalls);
    }

    [Fact]
    public async Task PortalCapture_WhenPipeWireFails_ReturnsStructuredFailureAndCleansUpSessionAndPipeWire()
    {
        var session = FakePortalScreenCastSessionFactory.CreateSession(width: 3, height: 2);
        var sessionFactory = new FakePortalScreenCastSessionFactory(PortalScreenCastSessionResult.Success(session));
        var pipeWireCapture = new FakePortalPipeWireFrameCapture(
            PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, "pipewire stream failed"));
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(pipeWireCapture);
        using var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Supported()),
            sessionFactory,
            pipeWireFactory);

        var result = await capture.CaptureAsync(ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Contains("pipewire stream failed", result.ErrorMessage);
        Assert.Equal(1, pipeWireFactory.CreateCalls);
        Assert.Equal(42U, pipeWireFactory.LastNodeId);
        Assert.Equal(3, pipeWireFactory.LastWidth);
        Assert.Equal(2, pipeWireFactory.LastHeight);
        Assert.Equal(1, pipeWireCapture.CaptureCalls);
        Assert.Equal(1, pipeWireCapture.DisposeCount);
        Assert.True(session.PipeWireRemote.IsClosed);
    }

    [Fact]
    public async Task PortalCapture_WhenPipeWireCancels_ReturnsCanceledAndCleansUpSessionAndPipeWire()
    {
        var session = FakePortalScreenCastSessionFactory.CreateSession(width: 3, height: 2);
        var sessionFactory = new FakePortalScreenCastSessionFactory(PortalScreenCastSessionResult.Success(session));
        var pipeWireCapture = new FakePortalPipeWireFrameCapture(
            PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, "should not return"))
        {
            CaptureException = new OperationCanceledException("pipewire canceled")
        };
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(pipeWireCapture);
        using var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Supported()),
            sessionFactory,
            pipeWireFactory);

        var result = await capture.CaptureAsync(ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(1, pipeWireFactory.CreateCalls);
        Assert.Equal(1, pipeWireCapture.CaptureCalls);
        Assert.Equal(1, pipeWireCapture.DisposeCount);
        Assert.True(session.PipeWireRemote.IsClosed);
    }

    [Fact]
    public async Task PortalCapture_WhenPipeWireTimesOut_ReturnsTimeoutAndCleansUpSessionAndPipeWire()
    {
        var session = FakePortalScreenCastSessionFactory.CreateSession(width: 3, height: 2);
        var sessionFactory = new FakePortalScreenCastSessionFactory(PortalScreenCastSessionResult.Success(session));
        var pipeWireCapture = new FakePortalPipeWireFrameCapture(
            PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, "should not return"))
        {
            CaptureException = new TimeoutException("pipewire timed out")
        };
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(pipeWireCapture);
        using var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Supported()),
            sessionFactory,
            pipeWireFactory);

        var result = await capture.CaptureAsync(ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureTimeout, result.ErrorKind);
        Assert.Contains("pipewire timed out", result.ErrorMessage);
        Assert.Equal(1, pipeWireFactory.CreateCalls);
        Assert.Equal(1, pipeWireCapture.CaptureCalls);
        Assert.Equal(1, pipeWireCapture.DisposeCount);
        Assert.True(session.PipeWireRemote.IsClosed);
    }

    [Fact]
    public async Task PortalCapture_WhenSessionGrantsAndPipeWireSucceeds_ReturnsFrameAndDisposesAdapters()
    {
        var owner = new CountingDisposable();
        var session = FakePortalScreenCastSessionFactory.CreateSession(width: 2, height: 1);
        var frame = ScreenReadingFrameFixtures.PortalFrame(
            new ScreenRect(0, 0, 2, 1),
            ScreenReadingFrameFixtures.TwoPixelXrgbBytes(),
            owner);
        var sessionFactory = new FakePortalScreenCastSessionFactory(PortalScreenCastSessionResult.Success(session));
        var pipeWireCapture = new FakePortalPipeWireFrameCapture(PortalPipeWireFrameResult.Success(frame));
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(pipeWireCapture);
        using var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Supported()),
            sessionFactory,
            pipeWireFactory);

        var result = await capture.CaptureAsync(ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        using var resultFrame = Assert.IsType<PortalPipeWireFrame>(result.Frame);
        Assert.Equal(new ScreenRect(0, 0, 2, 1), resultFrame.LogicalBounds);
        Assert.Equal(1, pipeWireCapture.DisposeCount);
        Assert.False(session.PipeWireRemote.IsClosed);
        Assert.Equal(0, owner.DisposeCount);

        resultFrame.Dispose();
        Assert.Equal(1, owner.DisposeCount);
        capture.Dispose();
        Assert.True(session.PipeWireRemote.IsClosed);
    }

    [Fact]
    public async Task PortalCapture_WhenStreamHasNonZeroPosition_ReturnsFrameAtPortalBounds()
    {
        var session = FakePortalScreenCastSessionFactory.CreateSession(x: -2, y: 3, width: 2, height: 1);
        var frame = ScreenReadingFrameFixtures.PortalFrame(
            new ScreenRect(0, 0, 2, 1),
            ScreenReadingFrameFixtures.TwoPixelXrgbBytes());
        var sessionFactory = new FakePortalScreenCastSessionFactory(PortalScreenCastSessionResult.Success(session));
        var pipeWireCapture = new FakePortalPipeWireFrameCapture(PortalPipeWireFrameResult.Success(frame));
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(pipeWireCapture);
        using var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Supported()),
            sessionFactory,
            pipeWireFactory);

        var result = await capture.CaptureAsync(ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        using var resultFrame = Assert.IsType<PortalPipeWireFrame>(result.Frame);
        Assert.Equal(new ScreenRect(-2, 3, 2, 1), resultFrame.LogicalBounds);
        Assert.Equal(new ScreenPixelColor(0x11, 0x22, 0x33), new ScreenFrame(resultFrame.LogicalBounds, resultFrame.Stride, resultFrame.PixelFormat, resultFrame.Pixels).GetPixel(new ScreenPoint(-2, 3)));
    }

    [Fact]
    public async Task PortalCapture_WhenMultipleMonitorStreamsAreAdjacent_ComposesLogicalFrame()
    {
        var streams = new[]
        {
            Stream(42, id: "left", x: 0, y: 0, width: 2, height: 1),
            Stream(43, id: "right", x: 2, y: 0, width: 2, height: 1)
        };
        var session = FakePortalScreenCastSessionFactory.CreateSession(streams);
        var leftCapture = new FakePortalPipeWireFrameCapture(PortalPipeWireFrameResult.Success(ScreenReadingFrameFixtures.PortalFrame(
            new ScreenRect(0, 0, 2, 1),
            ScreenReadingFrameFixtures.TwoPixelXrgbBytes())));
        var rightCapture = new FakePortalPipeWireFrameCapture(PortalPipeWireFrameResult.Success(ScreenReadingFrameFixtures.PortalFrame(
            new ScreenRect(0, 0, 2, 1),
            [0x99, 0x88, 0x77, 0x00, 0xCC, 0xBB, 0xAA, 0x00])));
        var sessionFactory = new FakePortalScreenCastSessionFactory(PortalScreenCastSessionResult.Success(session));
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(new Dictionary<uint, FakePortalPipeWireFrameCapture>
        {
            [42] = leftCapture,
            [43] = rightCapture
        });
        using var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Supported()),
            sessionFactory,
            pipeWireFactory);

        var result = await capture.CaptureAsync(ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        using var resultFrame = Assert.IsType<PortalPipeWireFrame>(result.Frame);
        using var screenFrame = new ScreenFrame(resultFrame.LogicalBounds, resultFrame.Stride, resultFrame.PixelFormat, resultFrame.Pixels);
        Assert.Equal(new ScreenRect(0, 0, 4, 1), resultFrame.LogicalBounds);
        Assert.Equal(new ScreenPixelColor(0x11, 0x22, 0x33), screenFrame.GetPixel(new ScreenPoint(0, 0)));
        Assert.Equal(new ScreenPixelColor(0x44, 0x55, 0x66), screenFrame.GetPixel(new ScreenPoint(1, 0)));
        Assert.Equal(new ScreenPixelColor(0x77, 0x88, 0x99), screenFrame.GetPixel(new ScreenPoint(2, 0)));
        Assert.Equal(new ScreenPixelColor(0xAA, 0xBB, 0xCC), screenFrame.GetPixel(new ScreenPoint(3, 0)));
        Assert.Equal([42U, 43U], pipeWireFactory.NodeIds);
    }

    [Fact]
    public async Task PortalCapture_WhenCapturingMultipleFrames_ReusesPortalSessionUntilDisposed()
    {
        var session = FakePortalScreenCastSessionFactory.CreateSession(width: 2, height: 1);
        var frame = ScreenReadingFrameFixtures.PortalFrame(
            new ScreenRect(0, 0, 2, 1),
            ScreenReadingFrameFixtures.TwoPixelXrgbBytes());
        var sessionFactory = new FakePortalScreenCastSessionFactory(PortalScreenCastSessionResult.Success(session));
        var pipeWireCapture = new FakePortalPipeWireFrameCapture(PortalPipeWireFrameResult.Success(frame));
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(pipeWireCapture);
        using var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Supported()),
            sessionFactory,
            pipeWireFactory);

        var first = await capture.CaptureAsync(ScreenReadOptions.Default);
        var second = await capture.CaptureAsync(ScreenReadOptions.Default);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(1, sessionFactory.StartCalls);
        Assert.Equal(2, pipeWireFactory.CreateCalls);
        Assert.False(session.PipeWireRemote.IsClosed);

        capture.Dispose();
        Assert.True(session.PipeWireRemote.IsClosed);
    }

    private static PortalStream Stream(uint nodeId, string id, int x, int y, int width, int height)
    {
        return new PortalStream(nodeId, new Dictionary<string, object>
        {
            ["source_type"] = 1U,
            ["id"] = id,
            ["position"] = new object[] { x, y },
            ["size"] = new object[] { width, height }
        });
    }
}
