using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class PortalScreenCastCapture : IPortalScreenCastCapture
{
    private readonly IPortalScreenCastSupportProbe _supportProbe;
    private readonly IPortalScreenCastSessionFactory _sessionFactory;
    private readonly IPortalPipeWireFrameCaptureFactory _pipeWireFactory;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private PortalScreenCastSession? _session;
    private bool _disposed;

    public PortalScreenCastCapture()
        : this(PortalScreenCastSupportProbe.Instance, PortalScreenCastDbusSessionFactory.Instance, PortalPipeWireFrameCaptureFactory.Instance)
    {
    }

    public PortalScreenCastCapture(
        IPortalScreenCastSupportProbe supportProbe,
        IPortalScreenCastSessionFactory sessionFactory,
        IPortalPipeWireFrameCaptureFactory pipeWireFactory)
    {
        _supportProbe = supportProbe ?? throw new ArgumentNullException(nameof(supportProbe));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _pipeWireFactory = pipeWireFactory ?? throw new ArgumentNullException(nameof(pipeWireFactory));
    }

    public PortalScreenCastSupportResult ProbeSupport() => _supportProbe.ProbeSupport();

    public async Task<PortalScreenCastCaptureResult> CaptureAsync(ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var support = ProbeSupport();
        if (!support.IsSupported)
        {
            return PortalScreenCastCaptureResult.Failure(
                support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                support.ErrorMessage ?? "XDG Desktop Portal ScreenCast is unavailable.");
        }

        return await CaptureSupportedAsync(options).ConfigureAwait(false);
    }

    public Task<PortalScreenCastCaptureResult> CaptureSupportedAsync(ScreenReadOptions options) =>
        CaptureSupportedAsync(null, options);

    public async Task<PortalScreenCastCaptureResult> CaptureSupportedAsync(ScreenRect? requestedRegion, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (options.CancellationToken.IsCancellationRequested)
        {
            return PortalScreenCastCaptureResult.Failure(ScreenReadErrorKind.Canceled, "XDG Desktop Portal ScreenCast capture was canceled before it started.");
        }

        try
        {
            var sessionResult = await GetOrStartSessionAsync(requestedRegion, options).ConfigureAwait(false);
            if (!sessionResult.IsSuccess)
            {
                return PortalScreenCastCaptureResult.Failure(
                    sessionResult.ErrorKind ?? ScreenReadErrorKind.CaptureFailed,
                    sessionResult.ErrorMessage ?? "XDG Desktop Portal ScreenCast session failed.");
            }

            return await CaptureSessionAsync(sessionResult.Session ?? throw new InvalidOperationException("Successful portal session did not include a session."), requestedRegion, options).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            DisposeCachedSession();
            return PortalScreenCastCaptureResult.Failure(ScreenReadErrorKind.Canceled, "XDG Desktop Portal ScreenCast capture was canceled.");
        }
        catch (TimeoutException ex)
        {
            DisposeCachedSession();
            return PortalScreenCastCaptureResult.Failure(ScreenReadErrorKind.CaptureTimeout, ex.Message);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException or ArgumentException or OverflowException)
        {
            DisposeCachedSession();
            return PortalScreenCastCaptureResult.Failure(ScreenReadErrorKind.CaptureFailed, ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeCachedSession();
        _sessionLock.Dispose();
    }

    private async Task<PortalScreenCastSessionResult> GetOrStartSessionAsync(ScreenRect? requestedRegion, ScreenReadOptions options)
    {
        await _sessionLock.WaitAsync(options.CancellationToken).ConfigureAwait(false);
        try
        {
            if (_session is not null)
            {
                var cachedValidation = PortalStreamGeometry.ValidateMonitorStreams(_session.Streams, requestedRegion);
                if (cachedValidation.IsSuccess)
                {
                    return PortalScreenCastSessionResult.Success(_session);
                }

                DisposeCachedSession();
                if (cachedValidation.ErrorKind != ScreenReadErrorKind.OutOfBounds)
                {
                    return PortalScreenCastSessionResult.Failure(
                        cachedValidation.ErrorKind ?? ScreenReadErrorKind.CaptureFailed,
                        cachedValidation.ErrorMessage ?? "Cached XDG Desktop Portal ScreenCast session contained unusable monitor metadata.");
                }
            }

            var sessionResult = await _sessionFactory.StartSessionAsync(requestedRegion, options).ConfigureAwait(false);
            if (sessionResult.IsSuccess)
            {
                _session = sessionResult.Session ?? throw new InvalidOperationException("Successful portal session did not include a session.");
            }

            return sessionResult;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task<PortalScreenCastCaptureResult> CaptureSessionAsync(PortalScreenCastSession session, ScreenRect? requestedRegion, ScreenReadOptions options)
    {
        var validation = PortalStreamGeometry.ValidateMonitorStreams(session.Streams, requestedRegion);
        if (!validation.IsSuccess)
        {
            DisposeCachedSession();
            return PortalScreenCastCaptureResult.Failure(
                validation.ErrorKind ?? ScreenReadErrorKind.CaptureFailed,
                validation.ErrorMessage ?? "XDG Desktop Portal ScreenCast returned unusable monitor metadata.");
        }

        var targetBounds = requestedRegion ?? validation.SelectedBounds ?? throw new InvalidOperationException("Validated portal streams did not include monitor bounds.");
        var streams = PortalStreamGeometry.GetIntersectingStreams(validation.Streams, targetBounds);
        if (streams.Count == 0)
        {
            DisposeCachedSession();
            return PortalScreenCastCaptureResult.Failure(
                ScreenReadErrorKind.OutOfBounds,
                "Requested region is outside validated XDG Desktop Portal monitor coverage. CrossMacro cannot force GNOME portal to select all monitors or a specific monitor; retry and select the monitor containing the requested coordinates.");
        }

        var result = streams.Count == 1 && streams[0].Bounds == targetBounds
            ? await CaptureWholeStreamAsync(session, streams[0], options).ConfigureAwait(false)
            : await CaptureComposedFrameAsync(session, streams, targetBounds, options).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            DisposeCachedSession();
        }

        return result;
    }

    private async Task<PortalScreenCastCaptureResult> CaptureWholeStreamAsync(PortalScreenCastSession session, PortalMonitorStream stream, ScreenReadOptions options)
    {
        var frameResult = await CaptureStreamFrameAsync(session, stream, options).ConfigureAwait(false);
        if (!frameResult.IsSuccess)
        {
            return PortalScreenCastCaptureResult.Failure(
                frameResult.ErrorKind ?? ScreenReadErrorKind.CaptureFailed,
                frameResult.ErrorMessage ?? "XDG Desktop Portal PipeWire capture failed.");
        }

        return PortalScreenCastCaptureResult.Success(frameResult.Frame ?? throw new InvalidOperationException("Successful PipeWire capture did not include a frame."));
    }

    private async Task<PortalScreenCastCaptureResult> CaptureComposedFrameAsync(
        PortalScreenCastSession session,
        IReadOnlyList<PortalMonitorStream> streams,
        ScreenRect targetBounds,
        ScreenReadOptions options)
    {
        ScreenPixelFormat? pixelFormat = null;
        byte[]? targetPixels = null;
        var targetStride = 0;

        foreach (var stream in streams)
        {
            var frameResult = await CaptureStreamFrameAsync(session, stream, options).ConfigureAwait(false);
            if (!frameResult.IsSuccess)
            {
                return PortalScreenCastCaptureResult.Failure(
                    frameResult.ErrorKind ?? ScreenReadErrorKind.CaptureFailed,
                    frameResult.ErrorMessage ?? "XDG Desktop Portal PipeWire capture failed.");
            }

            using var frame = frameResult.Frame ?? throw new InvalidOperationException("Successful PipeWire capture did not include a frame.");
            if (pixelFormat is null)
            {
                pixelFormat = frame.PixelFormat;
                targetStride = checked(targetBounds.Width * ScreenFrame.GetBytesPerPixel(frame.PixelFormat));
                targetPixels = new byte[checked(targetStride * targetBounds.Height)];
            }
            else if (pixelFormat.Value != frame.PixelFormat)
            {
                return PortalScreenCastCaptureResult.Failure(
                    ScreenReadErrorKind.CaptureFailed,
                    $"XDG Desktop Portal returned mixed PipeWire pixel formats '{pixelFormat.Value}' and '{frame.PixelFormat}'.");
            }

            CopyFrameIntersection(frame, targetBounds, targetPixels ?? throw new InvalidOperationException("Portal composition buffer was not initialized."), targetStride);
        }

        if (pixelFormat is null || targetPixels is null)
        {
            return PortalScreenCastCaptureResult.Failure(ScreenReadErrorKind.CaptureFailed, "XDG Desktop Portal did not provide any monitor streams to capture.");
        }

        return PortalScreenCastCaptureResult.Success(new PortalPipeWireFrame(targetBounds, targetStride, pixelFormat.Value, targetPixels));
    }

    private async Task<PortalPipeWireFrameResult> CaptureStreamFrameAsync(PortalScreenCastSession session, PortalMonitorStream stream, ScreenReadOptions options)
    {
        using var pipeWire = _pipeWireFactory.Create(
            session.PipeWireRemote,
            stream.Stream.NodeId,
            stream.Bounds.Width,
            stream.Bounds.Height);
        var frameResult = await pipeWire.CaptureFrameAsync(options).ConfigureAwait(false);
        if (!frameResult.IsSuccess)
        {
            return frameResult;
        }

        var frame = frameResult.Frame ?? throw new InvalidOperationException("Successful PipeWire capture did not include a frame.");
        return frame.LogicalBounds == stream.Bounds
            ? frameResult
            : PortalPipeWireFrameResult.Success(new PortalPipeWireFrame(stream.Bounds, frame.Stride, frame.PixelFormat, frame.Pixels, frame));
    }

    private static void CopyFrameIntersection(PortalPipeWireFrame source, ScreenRect targetBounds, byte[] targetPixels, int targetStride)
    {
        if (!PortalStreamGeometry.TryGetIntersection(source.LogicalBounds, targetBounds, out var intersection))
        {
            return;
        }

        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(source.PixelFormat);
        var sourceX = checked(intersection.X - source.LogicalBounds.X);
        var sourceY = checked(intersection.Y - source.LogicalBounds.Y);
        var targetX = checked(intersection.X - targetBounds.X);
        var targetY = checked(intersection.Y - targetBounds.Y);
        var rowBytes = checked(intersection.Width * bytesPerPixel);
        var sourcePixels = source.Pixels.Span;

        for (var row = 0; row < intersection.Height; row++)
        {
            var sourceOffset = checked((sourceY + row) * source.Stride + sourceX * bytesPerPixel);
            var targetOffset = checked((targetY + row) * targetStride + targetX * bytesPerPixel);
            sourcePixels.Slice(sourceOffset, rowBytes).CopyTo(targetPixels.AsSpan(targetOffset, rowBytes));
        }
    }

    private void DisposeCachedSession()
    {
        var session = Interlocked.Exchange(ref _session, null);
        session?.Dispose();
    }
}

internal sealed class PortalScreenCastException : Exception
{
    public PortalScreenCastException(ScreenReadErrorKind errorKind, string message)
        : base(message)
    {
        ErrorKind = errorKind;
    }

    public ScreenReadErrorKind ErrorKind { get; }
}
