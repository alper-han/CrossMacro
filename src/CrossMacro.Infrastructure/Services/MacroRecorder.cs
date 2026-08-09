
namespace CrossMacro.Infrastructure.Services;

public sealed class MacroRecorder(
    Func<IInputCapture>? inputCaptureFactory,
    ICoordinateStrategyFactory coordinateStrategyFactory,
    Func<ICoordinateStrategy, IInputEventProcessor> processorFactory,
    Func<IInputSimulator>? inputSimulatorFactory = null,
    IMousePositionProvider? positionProvider = null) : IMacroRecorder
{
    private static readonly TimeSpan CornerResetSettleDelay = TimeSpan.FromMilliseconds(32);

    private MacroSequence? _currentSequence;
    private ScreenRect? _recordingDesktopBounds;
    private Stopwatch? _stopwatch;
    private IInputCapture? _inputCapture;
    private readonly Lock _eventLock = new();
    private bool _isRecording;
    private long? _captureTimestampSourceEpochMicroseconds;
    private long _timelineTimestampAtCaptureEpochMicroseconds;
    private long _lastTimelineTimestampMicroseconds;

    private readonly Func<IInputCapture>? _inputCaptureFactory = inputCaptureFactory;
    private readonly ICoordinateStrategyFactory _coordinateStrategyFactory = coordinateStrategyFactory;
    private readonly Func<ICoordinateStrategy, IInputEventProcessor> _processorFactory = processorFactory;

    private readonly Func<IInputSimulator>? _inputSimulatorFactory = inputSimulatorFactory;
    private readonly IMousePositionProvider? _positionProvider = positionProvider;

    // Active components
    private ICoordinateStrategy? _currentStrategy;
    private IInputEventProcessor? _currentProcessor;

    public event EventHandler<MacroEventRecordedEventArgs>? EventRecorded;

    public bool IsRecording => Volatile.Read(ref _isRecording);

    public async Task StartRecordingAsync(bool recordMouse, bool recordKeyboard, IEnumerable<int>? ignoredKeys = null, bool forceRelative = false, bool skipInitialZero = false, CancellationToken cancellationToken = default)
    {
        if (!recordMouse && !recordKeyboard)
        {
            throw new ArgumentException("At least one recording type (mouse or keyboard) must be enabled", nameof(recordMouse));
        }

        bool requestedAbsoluteCoordinates = !forceRelative; // Strategy factory may adjust this based on platform capability.
        bool useAbsoluteCoordinates = requestedAbsoluteCoordinates;

        var ignoredKeysList = ignoredKeys?.ToList();
        Log.Debug("[MacroRecorder] Configuration: Mouse={Mouse}, Keyboard={Keyboard}, RequestedAbsolute={RequestedAbsolute}, ForceRelative={ForceRelative}, SkipInitialZero={SkipZero}, IgnoredKeys={IgnoredKeys}",
            recordMouse, recordKeyboard, useAbsoluteCoordinates, forceRelative, skipInitialZero,
            ignoredKeysList is not null ? string.Join(',', ignoredKeysList) : "none");

        using (_eventLock.EnterScope())
        {
            if (_isRecording)
            {
                return;
            }

            _isRecording = true;
            _currentSequence = new MacroSequence
            {
                Name = MacroNameDefaults.NewRecordedMacroName,
                CreatedAt = DateTime.UtcNow,
                IsAbsoluteCoordinates = useAbsoluteCoordinates,
                SkipInitialZeroZero = skipInitialZero,
            };
            _recordingDesktopBounds = null;
            _stopwatch = Stopwatch.StartNew();
            _captureTimestampSourceEpochMicroseconds = null;
            _timelineTimestampAtCaptureEpochMicroseconds = 0;
            _lastTimelineTimestampMicroseconds = 0;
        }

        try
        {
            if (_inputCaptureFactory is null)
            {
                throw new InvalidOperationException("No input capture factory configured. Please provide IInputCapture factory via DI.");
            }


            // 1. Initialize Strategy
            _currentStrategy = _coordinateStrategyFactory.Create(requestedAbsoluteCoordinates, forceRelative, skipInitialZero);
            useAbsoluteCoordinates = DetermineEffectiveAbsoluteCoordinates(requestedAbsoluteCoordinates, _currentStrategy);
            if (useAbsoluteCoordinates != requestedAbsoluteCoordinates)
            {
                Log.Information(
                    "[MacroRecorder] Coordinate mode auto-adjusted from {RequestedMode} to {EffectiveMode} based on strategy {StrategyType}.",
                    requestedAbsoluteCoordinates ? "absolute" : "relative",
                    useAbsoluteCoordinates ? "absolute" : "relative",
                    _currentStrategy.GetType().Name);
            }

            _currentSequence.IsAbsoluteCoordinates = useAbsoluteCoordinates;

            _currentProcessor = _processorFactory(_currentStrategy);
            _currentProcessor.Configure(
                recordMouse,
                recordKeyboard,
                ignoredKeys is not null ? new HashSet<int>(ignoredKeys) : null,
                useAbsoluteCoordinates);

            if (_currentStrategy is ICoordinateSampleSource sampleSource)
            {
                sampleSource.SampleAvailable += OnCoordinateSampleAvailable;
            }

            // 2. Perform Corner Reset for relative recordings when requested.
            if (!useAbsoluteCoordinates && !skipInitialZero)
            {
                await PerformCornerResetAsync(cancellationToken).ConfigureAwait(false);
            }

            await _currentStrategy.InitializeAsync(cancellationToken).ConfigureAwait(false);

            if (useAbsoluteCoordinates)
            {
                _recordingDesktopBounds = await TryGetDesktopBoundsAsync(cancellationToken).ConfigureAwait(false);
                if (_recordingDesktopBounds is { } bounds)
                {
                    Log.Information(
                        "[MacroRecorder] Absolute recording bounds: ({X},{Y}) {Width}x{Height}",
                        bounds.X,
                        bounds.Y,
                        bounds.Width,
                        bounds.Height);
                }
            }

            // 3. Initialize Capture
            _inputCapture = _inputCaptureFactory();
            var inputCapture = _inputCapture;
            var providerName = inputCapture.ProviderName;
            if (inputCapture is IMouseCoordinateModeInputCapture modeAwareCapture)
            {
                modeAwareCapture.ConfigureCoordinateMode(
                    useAbsoluteCoordinates,
                    _currentStrategy.ProducesLogicalCoordinates);
            }

            inputCapture.Configure(recordMouse, recordKeyboard);
            inputCapture.InputReceived += OnInputReceived;
            inputCapture.CaptureError += OnInputCaptureError;

            // StartAsync can complete after StopRecording() cleanup; keep a local reference to avoid races.
            Log.Information("[MacroRecorder] Recording started via {ProviderName}", providerName);
            await inputCapture.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            using (_eventLock.EnterScope())
            {
                _isRecording = false;
                _stopwatch?.Stop();
            }

            CleanupComponents();
            throw;
        }
    }

    private static void OnInputCaptureError(object? sender, InputCaptureErrorEventArgs e)
    {
        var errorMessage = e.Message;
        if (InputBackendErrorClassifier.IsKnownUnavailableMessage(errorMessage))
        {
            Log.Warning("[MacroRecorder] Input capture unavailable: {Error}", errorMessage);
            return;
        }

        Log.LogError("[MacroRecorder] Input capture error: {Error}", errorMessage);
    }


    private void OnInputReceived(object? sender, CapturedInputEventArgs e)
    {
        MacroEvent? recordedEvent = null;
        using (_eventLock.EnterScope())
        {
            if (!_isRecording || _currentSequence is null || _stopwatch is null || _currentProcessor is null)
            {
                return;
            }

            try
            {
                long timestampMicroseconds = ResolveCaptureTimestampMicroseconds(e.Event, _stopwatch);
                var macroEvent = _currentProcessor.Process(
                    e.Event,
                    MacroTiming.ToLegacyTimestampMilliseconds(timestampMicroseconds));

                if (macroEvent is { } currentEvent)
                {
                    currentEvent.TimestampMicroseconds = timestampMicroseconds;
                    recordedEvent = AddMacroEvent(currentEvent);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[MacroRecorder] Error processing input event");
            }
        }

        if (recordedEvent is not null)
        {
            PublishRecordedEvent(recordedEvent.Value);
        }
    }

    private void OnCoordinateSampleAvailable(object? sender, CoordinateSampleEventArgs e)
    {
        MacroEvent? recordedEvent = null;
        using (_eventLock.EnterScope())
        {
            if (!_isRecording || _currentSequence is null || _stopwatch is null || _currentProcessor is null)
            {
                return;
            }

            try
            {
                long timestampMicroseconds = EnsureMonotonicTimelineTimestamp(GetElapsedMicroseconds(_stopwatch));
                var macroEvent = _currentProcessor.ProcessPositionSample(
                    e.Sample,
                    MacroTiming.ToLegacyTimestampMilliseconds(timestampMicroseconds),
                    e.CoordinateSpace);
                if (macroEvent is { } currentEvent)
                {
                    currentEvent.TimestampMicroseconds = timestampMicroseconds;
                    recordedEvent = AddMacroEvent(currentEvent);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[MacroRecorder] Error processing coordinate sample");
            }
        }

        if (recordedEvent is not null)
        {
            PublishRecordedEvent(recordedEvent.Value);
        }
    }

    private MacroEvent AddMacroEvent(MacroEvent macroEvent)
    {
        macroEvent = NormalizeRecordedCoordinate(macroEvent);

        if (_currentSequence is null)
        {
            return macroEvent;
        }

        if (_currentSequence.Events.Count > 0)
        {
            var lastEvent = _currentSequence.Events[^1];
            long delayMicroseconds = Math.Max(
                0,
                macroEvent.TimestampMicroseconds - lastEvent.TimestampMicroseconds);
            macroEvent.DelayMicroseconds = delayMicroseconds;
        }
        else
        {
            macroEvent.DelayMicroseconds = 0;
        }

        _currentSequence.Events.Add(macroEvent);

        Log.Debug("[MacroRecorder] Event #{Count}: {Type} | X={X} Y={Y} | Key={Key} Button={Button} | Delay={Delay}us",
            _currentSequence.Events.Count, macroEvent.Type, macroEvent.X, macroEvent.Y,
            macroEvent.KeyCode, macroEvent.Button, macroEvent.DelayMicroseconds);

        return macroEvent;
    }

    private MacroEvent NormalizeRecordedCoordinate(MacroEvent macroEvent)
    {
        if (_recordingDesktopBounds is not { } bounds
            || _currentSequence is not { } sequence
            || MacroPositionSemantics.ResolveCoordinateMode(macroEvent, sequence.IsAbsoluteCoordinates) is not MouseCoordinateMode.Absolute
            || MacroPositionSemantics.ResolveCoordinateSpace(macroEvent, sequence.IsAbsoluteCoordinates) is not MouseCoordinateSpace.LogicalDesktop)
        {
            return macroEvent;
        }

        var normalized = bounds.Clamp(macroEvent.X, macroEvent.Y);
        if (normalized.X == macroEvent.X && normalized.Y == macroEvent.Y)
        {
            return macroEvent;
        }

        Log.Debug(
            "[MacroRecorder] Clamped absolute coordinate from ({OriginalX},{OriginalY}) to ({NormalizedX},{NormalizedY})",
            macroEvent.X,
            macroEvent.Y,
            normalized.X,
            normalized.Y);
        macroEvent.X = normalized.X;
        macroEvent.Y = normalized.Y;
        return macroEvent;
    }

    private static long GetElapsedMicroseconds(Stopwatch stopwatch) =>
        stopwatch.Elapsed.Ticks / (TimeSpan.TicksPerMillisecond / MacroTiming.MicrosecondsPerMillisecond);

    private long ResolveCaptureTimestampMicroseconds(CapturedInputEvent inputEvent, Stopwatch stopwatch)
    {
        var arrivalTimestampMicroseconds = GetElapsedMicroseconds(stopwatch);
        if (inputEvent.TimestampMicroseconds <= 0)
        {
            return EnsureMonotonicTimelineTimestamp(arrivalTimestampMicroseconds);
        }

        if (_captureTimestampSourceEpochMicroseconds is null)
        {
            _captureTimestampSourceEpochMicroseconds = inputEvent.TimestampMicroseconds;
            _timelineTimestampAtCaptureEpochMicroseconds = arrivalTimestampMicroseconds;
        }

        var sourceDeltaMicroseconds = inputEvent.TimestampMicroseconds - _captureTimestampSourceEpochMicroseconds.Value;
        if (sourceDeltaMicroseconds < 0)
        {
            Log.Warning(
                "[MacroRecorder] Capture timestamp regressed by {Delta}us; using arrival clock for this event.",
                sourceDeltaMicroseconds);
            return EnsureMonotonicTimelineTimestamp(arrivalTimestampMicroseconds);
        }

        return EnsureMonotonicTimelineTimestamp(checked(
            _timelineTimestampAtCaptureEpochMicroseconds + sourceDeltaMicroseconds));
    }

    private long EnsureMonotonicTimelineTimestamp(long timestampMicroseconds)
    {
        var normalized = Math.Max(_lastTimelineTimestampMicroseconds, timestampMicroseconds);
        _lastTimelineTimestampMicroseconds = normalized;
        return normalized;
    }

    private void PublishRecordedEvent(MacroEvent macroEvent)
    {
        try
        {
            EventRecorded?.Invoke(this, new MacroEventRecordedEventArgs(macroEvent));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[MacroRecorder] EventRecorded subscriber threw");
        }
    }

    public MacroSequence StopRecording()
    {
        MacroSequence? sequence;
        Stopwatch? stopwatch;

        using (_eventLock.EnterScope())
        {
            if (!_isRecording)
            {
                throw new InvalidOperationException("Not currently recording");
            }

            Log.Information("[MacroRecorder] Stopping recording...");

            _isRecording = false;
            stopwatch = _stopwatch;
            sequence = _currentSequence;
            stopwatch?.Stop();
        }

        CleanupComponents();

        if (sequence is not null && stopwatch is not null)
        {
            FinalizeSequence(sequence, stopwatch);
        }

        return sequence ?? new MacroSequence();
    }

    private static void FinalizeSequence(MacroSequence sequence, Stopwatch stopwatch)
    {
        sequence.CalculateDuration();
        sequence.RecordedAt = DateTime.UtcNow;
        sequence.ActualDuration = stopwatch.Elapsed;

        sequence.MouseMoveCount = sequence.Events.Count(e => e.Type is EventType.MouseMove);
        sequence.ClickCount = sequence.Events.Count(e =>
            e.Type is EventType.Click or EventType.ButtonPress or EventType.ButtonRelease);

        if (stopwatch.Elapsed.TotalSeconds > 0)
        {
            sequence.EventsPerSecond = sequence.Events.Count / stopwatch.Elapsed.TotalSeconds;
        }

        // Debug: Count event types
        var moveCount = sequence.Events.Count(e => e.Type is EventType.MouseMove);
        var buttonCount = sequence.Events.Count(e => e.Type is EventType.ButtonPress or EventType.ButtonRelease);
        var nonZeroMoves = sequence.Events.Where(e => e.Type is EventType.MouseMove && (e.X is not 0 || e.Y is not 0)).Take(5).ToList();

        Log.Information("[MacroRecorder] Recording completed: Duration={Duration:F2}s, TotalEvents={Events}, MouseMoves={Moves}, Buttons={Buttons}",
            stopwatch.Elapsed.TotalSeconds, sequence.Events.Count, moveCount, buttonCount);

        if (nonZeroMoves.Count > 0)
        {
            foreach (var m in nonZeroMoves)
            {
                Log.Debug("[MacroRecorder] Sample Move: X={X}, Y={Y}", m.X, m.Y);
            }
        }
        else if (moveCount > 0)
        {
            Log.Warning("[MacroRecorder] All {Count} MouseMove events have X=0 and Y=0!", moveCount);
        }
    }

    private void CleanupComponents()
    {
        if (_inputCapture is not null)
        {
            try
            {
                _inputCapture.InputReceived -= OnInputReceived;
                _inputCapture.CaptureError -= OnInputCaptureError;
                _inputCapture.StopCapture();
                _inputCapture.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[MacroRecorder] Error cleaning up input capture");
            }
            _inputCapture = null;
        }

        if (_currentStrategy is not null)
        {
            try
            {
                if (_currentStrategy is ICoordinateSampleSource sampleSource)
                {
                    sampleSource.SampleAvailable -= OnCoordinateSampleAvailable;
                }

                _currentStrategy.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[MacroRecorder] Error disposing strategy");
            }
            _currentStrategy = null;
        }
        _currentProcessor = null;
        _recordingDesktopBounds = null;
    }

    public MacroSequence? GetCurrentRecording()
    {
        return _currentSequence;
    }

    private static bool DetermineEffectiveAbsoluteCoordinates(bool requestedAbsoluteCoordinates, ICoordinateStrategy strategy)
    {
        if (!requestedAbsoluteCoordinates)
        {
            return false;
        }

        return !strategy.ProducesRelativeCoordinates;
    }

    private async Task PerformCornerResetAsync(CancellationToken cancellationToken)
    {
        if (_inputSimulatorFactory is null)
        {
            Log.Warning("[MacroRecorder] Relative recording requires corner reset, but no input simulator is available.");
            return;
        }

        try
        {
            Log.Information("[MacroRecorder] Performing desktop corner reset...");
            var desktopBounds = await TryGetDesktopBoundsAsync(cancellationToken).ConfigureAwait(false);
            using var simulator = _inputSimulatorFactory();
            await simulator.InitializeAsync(
                desktopBounds?.Width ?? 0,
                desktopBounds?.Height ?? 0,
                cancellationToken).ConfigureAwait(false);
            var expectedPosition = MouseCornerReset.MoveToDesktopOrigin(simulator, desktopBounds);
            await Task.Delay(
                CornerResetSettleDelay,
                TimeProvider.System,
                cancellationToken).ConfigureAwait(false);
            Log.Information(
                "[MacroRecorder] Corner Reset complete using {Mode} movement.",
                expectedPosition is null ? "relative fallback" : "absolute desktop-origin");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[MacroRecorder] Failed to perform Corner Reset");
        }
    }

    private async Task<ScreenRect?> TryGetDesktopBoundsAsync(CancellationToken cancellationToken)
    {
        if (_positionProvider is null)
        {
            return null;
        }

        try
        {
            return await _positionProvider.GetDesktopBoundsAsync()
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[MacroRecorder] Failed to resolve desktop bounds");
            return null;
        }
    }

    public void Dispose()
    {
        using (_eventLock.EnterScope())
        {
            _isRecording = false;
            _stopwatch?.Stop();
        }

        CleanupComponents();
    }
}
