
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
        Assert.Contains("portal unavailable", result.ErrorMessage, StringComparison.Ordinal);
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
        Assert.Contains("user denied", result.ErrorMessage, StringComparison.Ordinal);
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
        Assert.Contains("pipewire stream failed", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(1, pipeWireFactory.CreateCalls);
        Assert.Equal(42U, pipeWireFactory.LastNodeId);
        Assert.Equal(3, pipeWireFactory.LastWidth);
        Assert.Equal(2, pipeWireFactory.LastHeight);
        Assert.Equal(1, pipeWireCapture.CaptureCalls);
        Assert.Equal(1, pipeWireCapture.DisposeCount);
        Assert.True(session.PipeWireRemote.IsClosed);
    }

    [Fact]
    public async Task PortalCapture_WhenPipeWireCancels_ReturnsCanceledAndKeepsSessionAndPipeWire()
    {
        var session = FakePortalScreenCastSessionFactory.CreateSession(width: 3, height: 2);
        var sessionFactory = new FakePortalScreenCastSessionFactory(PortalScreenCastSessionResult.Success(session));
        var pipeWireCapture = new FakePortalPipeWireFrameCapture(
            PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, "should not return"))
        {
            CaptureException = new OperationCanceledException("pipewire canceled"),
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
        Assert.Equal(0, pipeWireCapture.DisposeCount);
        Assert.False(session.PipeWireRemote.IsClosed);

        capture.Dispose();
        Assert.Equal(1, pipeWireCapture.DisposeCount);
        Assert.True(session.PipeWireRemote.IsClosed);
    }

    [Fact]
    public async Task PortalCapture_WhenPipeWireTimesOut_ReturnsTimeoutAndKeepsSessionAndPipeWire()
    {
        var session = FakePortalScreenCastSessionFactory.CreateSession(width: 3, height: 2);
        var sessionFactory = new FakePortalScreenCastSessionFactory(PortalScreenCastSessionResult.Success(session));
        var pipeWireCapture = new FakePortalPipeWireFrameCapture(
            PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, "should not return"))
        {
            CaptureException = new TimeoutException("pipewire timed out"),
        };
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(pipeWireCapture);
        using var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Supported()),
            sessionFactory,
            pipeWireFactory);

        var result = await capture.CaptureAsync(ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureTimeout, result.ErrorKind);
        Assert.Contains("pipewire timed out", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(1, pipeWireFactory.CreateCalls);
        Assert.Equal(1, pipeWireCapture.CaptureCalls);
        Assert.Equal(0, pipeWireCapture.DisposeCount);
        Assert.False(session.PipeWireRemote.IsClosed);

        capture.Dispose();
        Assert.Equal(1, pipeWireCapture.DisposeCount);
        Assert.True(session.PipeWireRemote.IsClosed);
    }

    [Fact]
    public async Task PortalCapture_WhenSessionGrantsAndPipeWireSucceeds_ReturnsFrameAndKeepsAdaptersUntilDispose()
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
        Assert.Equal(0, pipeWireCapture.DisposeCount);
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
    public async Task PortalCapture_WhenRegionIsSmallerThanStream_RequestsOnlyLocalRegion()
    {
        var session = FakePortalScreenCastSessionFactory.CreateSession(x: -2, y: 3, width: 4, height: 2);
        var frame = ScreenReadingFrameFixtures.PortalFrame(
            new ScreenRect(0, 0, 1, 1),
            [0x33, 0x22, 0x11, 0x00]);
        var sessionFactory = new FakePortalScreenCastSessionFactory(PortalScreenCastSessionResult.Success(session));
        var pipeWireCapture = new FakePortalPipeWireFrameCapture(PortalPipeWireFrameResult.Success(frame));
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(pipeWireCapture);
        using var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Supported()),
            sessionFactory,
            pipeWireFactory);

        var result = await capture.CaptureSupportedAsync(new ScreenRect(-1, 4, 1, 1), ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        using var resultFrame = Assert.IsType<PortalPipeWireFrame>(result.Frame);
        Assert.Equal(new ScreenRect(-1, 4, 1, 1), resultFrame.LogicalBounds);
        Assert.Equal(new ScreenRect(1, 1, 1, 1), pipeWireCapture.LastRegion);
    }

    [Fact]
    public async Task PortalCapture_WhenRegionIsOutsideSelectedMonitors_PreservesSessionForLaterRequests()
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

        Assert.True((await capture.CaptureAsync(ScreenReadOptions.Default)).IsSuccess);
        var outside = await capture.CaptureSupportedAsync(new ScreenRect(10, 10, 1, 1), ScreenReadOptions.Default);
        var inside = await capture.CaptureSupportedAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(outside.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.OutOfBounds, outside.ErrorKind);
        Assert.True(inside.IsSuccess);
        Assert.Equal(1, sessionFactory.StartCalls);
        Assert.Equal(1, pipeWireFactory.CreateCalls);
        Assert.False(session.PipeWireRemote.IsClosed);
    }

    [Fact]
    public async Task PortalCapture_WhenStreamHasPipeWireSerial_ForwardsSerialToFactory()
    {
        var session = FakePortalScreenCastSessionFactory.CreateSession(
        [
            Stream(42, "monitor", 0, 0, 2, 1, pipeWireSerial: 777),
        ]);
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

        var result = await capture.CaptureSupportedAsync(ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        Assert.Equal(777UL, pipeWireFactory.LastPipeWireSerial);
    }

    [Fact]
    public async Task PortalCapture_WhenDisposedDuringCapture_CancelsCaptureAndDisposesOnce()
    {
        var session = FakePortalScreenCastSessionFactory.CreateSession(width: 2, height: 1);
        var pending = new TaskCompletionSource<PortalPipeWireFrameResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionFactory = new FakePortalScreenCastSessionFactory(PortalScreenCastSessionResult.Success(session));
        var pipeWireCapture = new FakePortalPipeWireFrameCapture(
            PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, "should not complete"))
        {
            PendingCapture = pending,
        };
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(pipeWireCapture);
        var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Supported()),
            sessionFactory,
            pipeWireFactory);

        var operation = capture.CaptureSupportedAsync(ScreenReadOptions.Default);
        while (pipeWireCapture.CaptureCalls is 0)
        {
            await Task.Yield();
        }

        capture.Dispose();
        var result = await operation;

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(1, pipeWireCapture.DisposeCount);
        Assert.True(session.PipeWireRemote.IsClosed);
    }

    [Fact]
    public async Task PortalCapture_WhenMultipleMonitorStreamsAreAdjacent_ComposesLogicalFrame()
    {
        var streams = new[]
        {
            Stream(42, id: "left", x: 0, y: 0, width: 2, height: 1),
            Stream(43, id: "right", x: 2, y: 0, width: 2, height: 1),
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
            [43] = rightCapture,
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
    public async Task PortalCapture_WhenStreamsHaveGap_MarksGapAsInvalid()
    {
        var streams = new[]
        {
            Stream(42, id: "left", x: 0, y: 0, width: 1, height: 1),
            Stream(43, id: "right", x: 2, y: 0, width: 1, height: 1),
        };
        var session = FakePortalScreenCastSessionFactory.CreateSession(streams);
        var leftCapture = new FakePortalPipeWireFrameCapture(PortalPipeWireFrameResult.Success(ScreenReadingFrameFixtures.PortalFrame(
            new ScreenRect(0, 0, 1, 1),
            [0x33, 0x22, 0x11, 0x00])));
        var rightCapture = new FakePortalPipeWireFrameCapture(PortalPipeWireFrameResult.Success(ScreenReadingFrameFixtures.PortalFrame(
            new ScreenRect(0, 0, 1, 1),
            [0xCC, 0xBB, 0xAA, 0x00])));
        var sessionFactory = new FakePortalScreenCastSessionFactory(PortalScreenCastSessionResult.Success(session));
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(new Dictionary<uint, FakePortalPipeWireFrameCapture>
        {
            [42] = leftCapture,
            [43] = rightCapture,
        });
        using var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Supported()),
            sessionFactory,
            pipeWireFactory);

        var result = await capture.CaptureAsync(ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        using var resultFrame = Assert.IsType<PortalPipeWireFrame>(result.Frame);
        using var screenFrame = new ScreenFrame(
            resultFrame.LogicalBounds,
            resultFrame.Stride,
            resultFrame.PixelFormat,
            resultFrame.Pixels,
            validPixelMask: resultFrame.ValidPixelMask);

        Assert.Equal([1, 0, 1], resultFrame.ValidPixelMask.ToArray());
        Assert.True(screenFrame.TryGetPixel(new ScreenPoint(0, 0), out _));
        Assert.False(screenFrame.TryGetPixel(new ScreenPoint(1, 0), out _));
        Assert.True(screenFrame.TryGetPixel(new ScreenPoint(2, 0), out _));
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
        Assert.Equal(1, pipeWireFactory.CreateCalls);
        Assert.False(session.PipeWireRemote.IsClosed);

        capture.Dispose();
        Assert.True(session.PipeWireRemote.IsClosed);
    }

    [Fact]
    public async Task PortalCapture_WhenSessionCloses_DoesNotReuseStalePermissionSession()
    {
        var firstSession = FakePortalScreenCastSessionFactory.CreateSession(width: 2, height: 1);
        var secondSession = FakePortalScreenCastSessionFactory.CreateSession(width: 2, height: 1);
        var sessionFactory = new SessionSequenceFactory(firstSession, secondSession);
        var frame = ScreenReadingFrameFixtures.PortalFrame(
            new ScreenRect(0, 0, 2, 1),
            ScreenReadingFrameFixtures.TwoPixelXrgbBytes());
        var pipeWireCapture = new FakePortalPipeWireFrameCapture(PortalPipeWireFrameResult.Success(frame));
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(pipeWireCapture);
        using var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Supported()),
            sessionFactory,
            pipeWireFactory);

        var first = await capture.CaptureAsync(ScreenReadOptions.Default);
        Assert.True(first.IsSuccess);
        firstSession.MarkClosed();

        var second = await capture.CaptureAsync(ScreenReadOptions.Default);

        Assert.True(second.IsSuccess);
        Assert.Equal(2, sessionFactory.StartCalls);
        Assert.Equal(2, pipeWireFactory.CreateCalls);
        Assert.True(firstSession.PipeWireRemote.IsClosed);
        Assert.False(secondSession.PipeWireRemote.IsClosed);
    }

    [Fact]
    public async Task PortalCapture_WhenSessionClosesDuringFrame_DoesNotReturnStaleFrame()
    {
        var firstSession = FakePortalScreenCastSessionFactory.CreateSession(width: 2, height: 1);
        var secondSession = FakePortalScreenCastSessionFactory.CreateSession(width: 2, height: 1);
        var sessionFactory = new SessionSequenceFactory(firstSession, secondSession);
        var frame = ScreenReadingFrameFixtures.PortalFrame(
            new ScreenRect(0, 0, 2, 1),
            ScreenReadingFrameFixtures.TwoPixelXrgbBytes());
        var pipeWireCapture = new FakePortalPipeWireFrameCapture(PortalPipeWireFrameResult.Success(frame))
        {
            CaptureStarted = firstSession.MarkClosed,
        };
        var pipeWireFactory = new FakePortalPipeWireFrameCaptureFactory(pipeWireCapture);
        using var capture = new PortalScreenCastCapture(
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Supported()),
            sessionFactory,
            pipeWireFactory);

        var first = await capture.CaptureAsync(ScreenReadOptions.Default);
        var second = await capture.CaptureAsync(ScreenReadOptions.Default);

        Assert.False(first.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, first.ErrorKind);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, sessionFactory.StartCalls);
        Assert.True(firstSession.PipeWireRemote.IsClosed);
        Assert.False(secondSession.PipeWireRemote.IsClosed);
    }

    private static PortalStreamDescriptor Stream(uint nodeId, string id, int x, int y, int width, int height, ulong? pipeWireSerial = null)
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["source_type"] = 1U,
            ["id"] = id,
            ["position"] = new object[] { x, y },
            ["size"] = new object[] { width, height },
        };
        if (pipeWireSerial is { } serial)
        {
            properties["pipewire-serial"] = serial;
        }

        return new PortalStreamDescriptor(nodeId, properties);
    }

    private sealed class SessionSequenceFactory(params PortalScreenCastSession[] sessions) : IPortalScreenCastSessionFactory
    {
        private readonly Queue<PortalScreenCastSession> _sessions = new(sessions);

        public int StartCalls { get; private set; }

        public Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenReadOptions options)
            => StartSessionAsync(requestedRegion: null, options);

        public Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenRect? requestedRegion, ScreenReadOptions options)
        {
            _ = requestedRegion;
            _ = options;
            StartCalls++;
            return Task.FromResult(PortalScreenCastSessionResult.Success(_sessions.Dequeue()));
        }
    }
}
