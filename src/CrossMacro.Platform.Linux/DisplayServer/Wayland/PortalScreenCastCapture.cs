
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class PortalScreenCastCapture(
    IPortalScreenCastSupportProbe supportProbe,
    IPortalScreenCastSessionFactory sessionFactory,
    IPortalPipeWireFrameCaptureFactory pipeWireFactory) : IPortalScreenCastCapture
{
    private readonly IPortalScreenCastSupportProbe _supportProbe = supportProbe ?? throw new ArgumentNullException(nameof(supportProbe));
    private readonly IPortalScreenCastSessionFactory _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    private readonly IPortalPipeWireFrameCaptureFactory _pipeWireFactory = pipeWireFactory ?? throw new ArgumentNullException(nameof(pipeWireFactory));
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly Lock _captureAdmissionGate = new();
    private readonly Dictionary<(uint NodeId, ulong? PipeWireSerial, ScreenRect Bounds), IPortalPipeWireFrameCapture> _pipeWireCaptures = [];
    private TaskCompletionSource _captureAdmissionsDrained = CreateCompletedSource();
    private PortalScreenCastSession? _session;
    private int _captureAdmissions;
    private int _disposeState;

    public PortalScreenCastCapture()
        : this(PortalScreenCastSupportProbe.Instance, PortalScreenCastDbusSessionFactory.Instance, PortalPipeWireFrameCaptureFactory.Instance) { /* Empty */ }

    public PortalScreenCastSupportResult ProbeSupport() => _supportProbe.ProbeSupport();

    public async Task<PortalScreenCastCaptureResult> CaptureAsync(ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) is not 0, this);

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
        CaptureSupportedAsync(region: null, options);

    public async Task<PortalScreenCastCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) is not 0, this);

        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            options.CancellationToken,
            _disposeCancellation.Token);
        var operationOptions = new ScreenReadOptions(
            options.Timeout,
            options.PollInterval,
            options.PollUntilMatch,
            operationCancellation.Token);

        if (operationOptions.CancellationToken.IsCancellationRequested)
        {
            return PortalScreenCastCaptureResult.Failure(ScreenReadErrorKind.Canceled, "XDG Desktop Portal ScreenCast capture was canceled before it started.");
        }

        var admitted = false;
        var acquired = false;
        try
        {
            await EnterCaptureAsync(operationOptions.CancellationToken).ConfigureAwait(false);
            admitted = true;
            await _captureLock.WaitAsync(operationOptions.CancellationToken).ConfigureAwait(false);
            acquired = true;
            try
            {
                var sessionResult = await GetOrStartSessionAsync(region, operationOptions).ConfigureAwait(false);
                if (!sessionResult.IsSuccess)
                {
                    return PortalScreenCastCaptureResult.Failure(
                        sessionResult.ErrorKind ?? ScreenReadErrorKind.CaptureFailed,
                        sessionResult.ErrorMessage ?? "XDG Desktop Portal ScreenCast session failed.");
                }

                return await CaptureSessionAsync(sessionResult.Session ?? throw new InvalidOperationException("Successful portal session did not include a session."), region, operationOptions).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return PortalScreenCastCaptureResult.Failure(ScreenReadErrorKind.Canceled, "XDG Desktop Portal ScreenCast capture was canceled.");
            }
            catch (TimeoutException ex)
            {
                return PortalScreenCastCaptureResult.Failure(ScreenReadErrorKind.CaptureTimeout, ex.Message);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException or ArgumentException or OverflowException)
            {
                DisposeCachedSession();
                return PortalScreenCastCaptureResult.Failure(ScreenReadErrorKind.CaptureFailed, ex.Message);
            }
        }
        finally
        {
            if (acquired)
            {
                _ = _captureLock.Release();
            }

            if (admitted)
            {
                ExitCapture();
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) is not 0)
        {
            return;
        }

        _disposeCancellation.Cancel();
        Task drained;
        lock (_captureAdmissionGate)
        {
            drained = _captureAdmissionsDrained.Task;
        }

        drained.GetAwaiter().GetResult();
        try
        {
            DisposeCachedSession();
        }
        finally
        {
            _captureLock.Dispose();
            _sessionLock.Dispose();
            _disposeCancellation.Dispose();
        }
    }

    private Task EnterCaptureAsync(CancellationToken cancellationToken)
    {
        lock (_captureAdmissionGate)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) is not 0, this);
            if (_captureAdmissions++ is 0)
            {
                _captureAdmissionsDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
        catch
        {
            ExitCapture();
            throw;
        }
    }

    private void ExitCapture()
    {
        lock (_captureAdmissionGate)
        {
            if (--_captureAdmissions is 0)
            {
                _ = _captureAdmissionsDrained.TrySetResult();
            }
        }
    }

    private static TaskCompletionSource CreateCompletedSource()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = source.TrySetResult();
        return source;
    }

    private async Task<PortalScreenCastSessionResult> GetOrStartSessionAsync(ScreenRect? region, ScreenReadOptions options)
    {
        await _sessionLock.WaitAsync(options.CancellationToken).ConfigureAwait(false);
        try
        {
            if (_session is not null && !_session.IsClosed)
            {
                var cachedValidation = PortalStreamGeometry.ValidateMonitorStreams(_session.Streams, region);
                if (cachedValidation.IsSuccess)
                {
                    return PortalScreenCastSessionResult.Success(_session);
                }

                var cachedErrorKind = cachedValidation.ErrorKind.GetValueOrDefault(ScreenReadErrorKind.CaptureFailed);
                if (cachedErrorKind is ScreenReadErrorKind.OutOfBounds)
                {
                    return PortalScreenCastSessionResult.Failure(
                        cachedErrorKind,
                        cachedValidation.ErrorMessage ?? "Cached XDG Desktop Portal ScreenCast session contained unusable monitor metadata.");
                }

                DisposeCachedSession();
                return PortalScreenCastSessionResult.Failure(
                    cachedErrorKind,
                    cachedValidation.ErrorMessage ?? "Cached XDG Desktop Portal ScreenCast session contained unusable monitor metadata.");
            }

            if (_session is not null)
            {
                DisposeCachedSession();
            }

            var sessionResult = await _sessionFactory.StartSessionAsync(region, options).ConfigureAwait(false);
            if (sessionResult.IsSuccess)
            {
                _session = sessionResult.Session ?? throw new InvalidOperationException("Successful portal session did not include a session.");
            }

            return sessionResult;
        }
        finally
        {
            _ = _sessionLock.Release();
        }
    }

    private async Task<PortalScreenCastCaptureResult> CaptureSessionAsync(PortalScreenCastSession session, ScreenRect? region, ScreenReadOptions options)
    {
        var validation = PortalStreamGeometry.ValidateMonitorStreams(session.Streams, region);
        if (!validation.IsSuccess)
        {
            if (validation.ErrorKind is not ScreenReadErrorKind.OutOfBounds)
            {
                DisposeCachedSession();
            }

            return PortalScreenCastCaptureResult.Failure(
                validation.ErrorKind ?? ScreenReadErrorKind.CaptureFailed,
                validation.ErrorMessage ?? "XDG Desktop Portal ScreenCast returned unusable monitor metadata.");
        }

        var targetBounds = region ?? validation.SelectedBounds ?? throw new InvalidOperationException("Validated portal streams did not include monitor bounds.");
        var streams = PortalStreamGeometry.GetIntersectingStreams(validation.Streams, targetBounds);
        if (streams.Count is 0)
        {
            DisposeCachedSession();
            return PortalScreenCastCaptureResult.Failure(
                ScreenReadErrorKind.OutOfBounds,
                "Requested region is outside validated XDG Desktop Portal monitor coverage. CrossMacro cannot force the portal to select all monitors or a specific monitor; retry and select the monitor containing the requested coordinates.");
        }

        var result = streams.Count is 1 && streams[0].Bounds == targetBounds
            ? await CaptureWholeStreamAsync(session, streams[0], targetBounds, options).ConfigureAwait(false)
            : await CaptureComposedFrameAsync(session, streams, targetBounds, options).ConfigureAwait(false);

        if (!result.IsSuccess && result.ErrorKind is not (ScreenReadErrorKind.CaptureTimeout or ScreenReadErrorKind.Canceled))
        {
            DisposeCachedSession();
        }

        return result;
    }

    private async Task<PortalScreenCastCaptureResult> CaptureWholeStreamAsync(
        PortalScreenCastSession session,
        PortalMonitorStream stream,
        ScreenRect targetBounds,
        ScreenReadOptions options)
    {
        var frameResult = await CaptureStreamFrameAsync(session, stream, targetBounds, options).ConfigureAwait(false);
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
        byte[]? targetValidPixelMask = null;
        var targetStride = 0;

        foreach (var stream in streams)
        {
            var intersection = PortalStreamGeometry.TryGetIntersection(stream.Bounds, targetBounds, out var streamIntersection)
                ? streamIntersection
                : stream.Bounds;
            var frameResult = await CaptureStreamFrameAsync(session, stream, intersection, options).ConfigureAwait(false);
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
                targetValidPixelMask = new byte[checked(targetBounds.Width * targetBounds.Height)];
            }
            else if (pixelFormat.Value != frame.PixelFormat)
            {
                return PortalScreenCastCaptureResult.Failure(
                    ScreenReadErrorKind.CaptureFailed,
                    $"XDG Desktop Portal returned mixed PipeWire pixel formats '{pixelFormat.Value}' and '{frame.PixelFormat}'.");
            }

            CopyFrameIntersection(
                frame,
                targetBounds,
                targetPixels ?? throw new InvalidOperationException("Portal composition buffer was not initialized."),
                targetValidPixelMask ?? throw new InvalidOperationException("Portal validity buffer was not initialized."),
                targetStride);
        }

        if (pixelFormat is null || targetPixels is null || targetValidPixelMask is null)
        {
            return PortalScreenCastCaptureResult.Failure(ScreenReadErrorKind.CaptureFailed, "XDG Desktop Portal did not provide any monitor streams to capture.");
        }

        return PortalScreenCastCaptureResult.Success(new PortalPipeWireFrame(targetBounds, targetStride, pixelFormat.Value, targetPixels, validPixelMask: targetValidPixelMask));
    }

    private async Task<PortalPipeWireFrameResult> CaptureStreamFrameAsync(
        PortalScreenCastSession session,
        PortalMonitorStream stream,
        ScreenRect requestedRegion,
        ScreenReadOptions options)
    {
        if (session.IsClosed)
        {
            return PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, "XDG Desktop Portal ScreenCast session closed before the PipeWire capture started.");
        }

        var target = PortalStreamGeometry.TryGetIntersection(stream.Bounds, requestedRegion, out var requestedIntersection)
            ? requestedIntersection
            : stream.Bounds;
        var localRegion = new ScreenRect(
            checked(target.X - stream.Bounds.X),
            checked(target.Y - stream.Bounds.Y),
            target.Width,
            target.Height);
        var pipeWire = GetPipeWireCapture(session, stream);
        var frameResult = await pipeWire.CaptureFrameAsync(localRegion, options).ConfigureAwait(false);
        if (!frameResult.IsSuccess)
        {
            return frameResult;
        }

        if (session.IsClosed)
        {
            return PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, "XDG Desktop Portal ScreenCast session closed while the PipeWire frame was being captured.");
        }

        var frame = frameResult.Frame ?? throw new InvalidOperationException("Successful PipeWire capture did not include a frame.");
        var globalBounds = target;
        return globalBounds == frame.LogicalBounds
            ? frameResult
            : PortalPipeWireFrameResult.Success(new PortalPipeWireFrame(globalBounds, frame.Stride, frame.PixelFormat, frame.Pixels, frame, frame.ValidPixelMask));
    }

    private IPortalPipeWireFrameCapture GetPipeWireCapture(PortalScreenCastSession session, PortalMonitorStream stream)
    {
        var key = (stream.Stream.NodeId, stream.Stream.PipeWireSerial, stream.Bounds);
        if (_pipeWireCaptures.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var capture = _pipeWireFactory.Create(
            session.PipeWireRemote,
            stream.Stream,
            stream.Bounds.Width,
            stream.Bounds.Height);
        _pipeWireCaptures.Add(key, capture);
        return capture;
    }

    private static void CopyFrameIntersection(
        PortalPipeWireFrame source,
        ScreenRect targetBounds,
        byte[] targetPixels,
        byte[] targetValidPixelMask,
        int targetStride)
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
        var sourceMask = source.ValidPixelMask.Span;

        for (var row = 0; row < intersection.Height; row++)
        {
            var sourceOffset = checked(((sourceY + row) * source.Stride) + (sourceX * bytesPerPixel));
            var targetOffset = checked(((targetY + row) * targetStride) + (targetX * bytesPerPixel));
            sourcePixels.Slice(sourceOffset, rowBytes).CopyTo(targetPixels.AsSpan(targetOffset, rowBytes));

            var targetMaskOffset = checked(((targetY + row) * targetBounds.Width) + targetX);
            if (source.ValidPixelMask.IsEmpty)
            {
                targetValidPixelMask.AsSpan(targetMaskOffset, intersection.Width).Fill(1);
            }
            else
            {
                var sourceMaskOffset = checked(((sourceY + row) * source.LogicalBounds.Width) + sourceX);
                sourceMask.Slice(sourceMaskOffset, intersection.Width).CopyTo(targetValidPixelMask.AsSpan(targetMaskOffset, intersection.Width));
            }
        }
    }

    private void DisposeCachedSession()
    {
        var session = Interlocked.Exchange(ref _session, value: null);
        foreach (var capture in _pipeWireCaptures.Values)
        {
            capture.Dispose();
        }

        _pipeWireCaptures.Clear();
        session?.Dispose();
    }
}
