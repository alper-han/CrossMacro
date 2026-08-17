
namespace CrossMacro.Infrastructure.Services;


public sealed class MacroPlayer : IMacroPlayer, IPlaybackPauseToken, IRunScriptRuntimeVariableSource
{
    private readonly IMousePositionProvider? _positionProvider;
    private readonly IScreenPixelReader _screenPixelReader;
    private readonly IWindowManager _windowManager;
    private readonly IClipboardService? _clipboardService;
    private readonly IShellCommandRunner? _shellCommandRunner;
    private readonly IScreenshotCaptureService? _screenshotCaptureService;
    private readonly IImageClickMovementResolver _imageClickMovementResolver;
    private readonly IImageAssetCodec _imageAssetCodec;
    private readonly IPlaybackValidator _validator;
    private readonly IPlaybackTimingService _timingService;
    private readonly Func<TimeSpan, CancellationToken, Task> _playbackWaitAsync;
    private readonly Func<Func<double>> _playbackElapsedMillisecondsFactory;
    private readonly Func<IPlaybackCoordinator> _coordinatorFactory;
    private readonly Func<IButtonStateTracker> _buttonTrackerFactory;
    private readonly Func<IKeyStateTracker> _keyTrackerFactory;
    private readonly IPlaybackMouseButtonMapper _buttonMapper;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly PlaybackDelayResolver _delayResolver;
    private readonly PlaybackSessionResourceOwner _session;

    private IInputSimulator? _inputSimulator;
    private MacroEventExecutor? _eventExecutor;
    private IPlaybackCoordinator? _coordinator;

    private bool _disposed;

    private int _cachedScreenWidth;
    private int _cachedScreenHeight;
    private ScreenRect? _cachedDesktopBounds;
    private bool _resolutionCached;

    private int _errorCount;
    private readonly IDictionary<string, string> _runtimeVariables;

    private const int MaxPlaybackErrors = 10;
    private const double MinCatchUpResetDriftMs = 30.0;
    private const double CatchUpResetDelayMultiplier = 2.0;
    private const int MaxDaemonTrajectorySamples = 128;
    private const long MaxDaemonTrajectoryDurationMicroseconds = 20_000;

    private sealed class PlaybackRunState
    {
        public int EventCount;
        public double ScheduledElapsedMs;
        public double TimelineAnchorElapsedMs;
        public bool HasTimelineAnchor;
        public int ObservedPauseResumeVersion;
        // Logical desktop motion may be applied asynchronously; verify it before pointer-dependent work.
        public bool HasPendingLogicalMove;
        public int PendingLogicalX;
        public int PendingLogicalY;
        public int TrajectorySampleCount;
        public int DirectLogicalMoveCount;
    }

    // Pause support

    public bool IsPlaying { get; private set; }
    public int CurrentLoop { get; private set; }
    public int TotalLoops { get; private set; }
    public bool IsWaitingBetweenLoops { get; private set; }
    public bool IsPaused => _session.IsPaused;
    public IReadOnlyDictionary<string, string> RuntimeVariables => _session.RuntimeVariables;

    /// <summary>
    /// Creates a new MacroPlayer from explicitly composed collaborators.
    /// </summary>
    public MacroPlayer(IPlaybackValidator validator, MacroPlayerDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        _positionProvider = dependencies.PositionProvider;
        _screenPixelReader = dependencies.ScreenPixelReader;
        _windowManager = dependencies.WindowManager;
        _clipboardService = dependencies.ClipboardService;
        _shellCommandRunner = dependencies.ShellCommandRunner;
        _screenshotCaptureService = dependencies.ScreenshotCaptureService;
        _imageClickMovementResolver = dependencies.ImageClickMovementResolver;
        _imageAssetCodec = dependencies.ImageAssetCodec;
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timingService = dependencies.TimingService;
        _playbackWaitAsync = dependencies.PlaybackWaitAsync;
        _playbackElapsedMillisecondsFactory = dependencies.PlaybackElapsedMillisecondsFactory;
        _coordinatorFactory = dependencies.CoordinatorFactory;
        _buttonTrackerFactory = dependencies.ButtonTrackerFactory;
        _keyTrackerFactory = dependencies.KeyTrackerFactory;
        _buttonMapper = dependencies.ButtonMapper;
        _keyCodeMapper = dependencies.KeyCodeMapper;
        _delayResolver = dependencies.DelayResolver;
        _session = new PlaybackSessionResourceOwner(dependencies.InputSimulatorFactory, dependencies.SimulatorPool);
        _runtimeVariables = _session.Variables;

        if (_positionProvider is not null)
        {
            if (_positionProvider.IsSupported)
            {
                Log.Debug("[MacroPlayer] Using position provider: {ProviderName}", _positionProvider.ProviderName);
            }
            else
            {
                Log.Warning("[MacroPlayer] Position provider not supported, using relative coordinates");
            }
        }

        if (dependencies.SimulatorPool is not null)
        {
            Log.Debug("[MacroPlayer] Using InputSimulatorPool for zero-delay device acquisition");
        }
    }

    #region IPlaybackPauseToken Implementation

    bool IPlaybackPauseToken.IsPaused => _session.IsPaused;

    async Task IPlaybackPauseToken.WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        await _session.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion

    public async Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(macro);

        if (IsPlaying)
        {
            throw new InvalidOperationException("Playback is already in progress");
        }

        var validationResult = _validator.Validate(macro);
        if (!validationResult.IsValid)
        {
            var errorMsg = string.Join(", ", validationResult.Errors);
            Log.LogError("[MacroPlayer] Validation failed: {Error}", errorMsg);
            throw new InvalidOperationException($"Playback validation failed: {errorMsg}");
        }

        foreach (var warning in validationResult.Warnings)
        {
            Log.Warning("[MacroPlayer] Warning: {Warning}", warning);
        }

        var requestedOptions = options ?? new PlaybackOptions();
        requestedOptions.Normalize();
        var precisionRatePlan = PrecisionMotionRateGovernor.CreatePlan(macro, requestedOptions);
        var effectiveOptions = ClonePlaybackOptions(requestedOptions, precisionRatePlan.EffectiveSpeedMultiplier);

        if (precisionRatePlan.IsQualityLimited)
        {
            Log.Information(
                "[MacroPlayer] Precision quality lock: RequestedSpeed={RequestedSpeed:F3}x, EffectiveSpeed={EffectiveSpeed:F3}x, SourcePeakReportsPerSecond={SourcePeakReportsPerSecond:F1}, CapReportsPerSecond={CapReportsPerSecond}",
                precisionRatePlan.RequestedSpeedMultiplier,
                precisionRatePlan.EffectiveSpeedMultiplier,
                precisionRatePlan.SourcePeakEventsPerSecond,
                precisionRatePlan.OutputCapEventsPerSecond);
        }

        var lifecycle = new MacroPlaybackLifecycle(
            BeginPlayback,
            () => _session.Token,
            CleanupAsync,
            SetLoopProgress,
            waiting => IsWaitingBetweenLoops = waiting,
            HasOnlyRuntimeScriptSteps,
            HasRuntimeScriptSteps,
            ExecuteScreenReadScriptStepsAsync,
            SetupRuntimeScriptOnlyAsync,
            (sequence, _) => SetupPlaybackAsync(sequence),
            PrepareIterationAsync,
            iteration => Log.Information("[MacroPlayer] Starting playback iteration {Iteration}", iteration),
            (playbackOptions, repeatCount, infiniteLoop) => Log.Information(
                "[MacroPlayer] Loop settings: Loop={Loop}, RepeatCount={Count}, Infinite={Infinite}",
                playbackOptions.Loop,
                repeatCount,
                infiniteLoop),
            PlayOnceAsync,
            PlayOnceRuntimeScriptAsync,
            WaitForStabilizationAsync,
            ResolveTrailingDelayMicroseconds,
            ResolveRepeatDelayMs,
            (delayMs, token) => _timingService.WaitAsync(delayMs, this, token));

        await lifecycle.RunAsync(macro, effectiveOptions, cancellationToken).ConfigureAwait(false);
    }

    private static PlaybackOptions ClonePlaybackOptions(PlaybackOptions source, double speedMultiplier)
    {
        return new PlaybackOptions
        {
            SpeedMultiplier = speedMultiplier,
            Loop = source.Loop,
            RepeatCount = source.RepeatCount,
            RepeatDelayMs = source.RepeatDelayMs,
            UseRandomRepeatDelay = source.UseRandomRepeatDelay,
            RepeatDelayMinMs = source.RepeatDelayMinMs,
            RepeatDelayMaxMs = source.RepeatDelayMaxMs,
            MotionMode = source.MotionMode,
            StrictSpeedMotionEventsPerSecond = source.StrictSpeedMotionEventsPerSecond,
            PrecisionMotionEventsPerSecond = source.PrecisionMotionEventsPerSecond,
            MaximumMotionErrorPixels = source.MaximumMotionErrorPixels,
        };
    }

    private void BeginPlayback(CancellationToken cancellationToken)
    {
        _session.Begin(cancellationToken);
        _inputSimulator = null;
        _cachedDesktopBounds = null;
        _cachedScreenWidth = 0;
        _cachedScreenHeight = 0;
        _resolutionCached = false;
        IsPlaying = true;
        _errorCount = 0;
        _runtimeVariables.Clear();
        Log.Information("[MacroPlayer] ========== PLAYBACK STARTED ==========");
    }

    private void SetLoopProgress(int totalLoops, int currentLoop)
    {
        TotalLoops = totalLoops;
        CurrentLoop = currentLoop;
    }

    private async Task SetupPlaybackAsync(MacroSequence macro)
    {
        await CacheResolutionAsync().ConfigureAwait(false);
        await AcquireSimulatorAsync(macro).ConfigureAwait(false);
        EnsureAbsolutePlaybackSupported(macro);
        await InitializePlaybackComponentsAsync(macro).ConfigureAwait(false);
    }

    private async Task PrepareIterationAsync(int iteration, MacroSequence macro, CancellationToken cancellationToken)
    {
        var coordinator = _coordinator ?? throw new InvalidOperationException("Coordinator is not initialized.");
        var inputSimulator = _inputSimulator ?? throw new InvalidOperationException("Input simulator is not initialized.");
        await coordinator.PrepareIterationAsync(
            iteration,
            macro,
            inputSimulator,
            _cachedScreenWidth,
            _cachedScreenHeight,
            cancellationToken).ConfigureAwait(false);
    }

    private Task WaitForStabilizationAsync(CancellationToken cancellationToken)
    {
        return _playbackWaitAsync(TimeSpan.FromMilliseconds(50), cancellationToken);
    }

    private long ResolveTrailingDelayMicroseconds(MacroSequence macro)
    {
        var randomDelayMicroseconds = macro.HasTrailingRandomDelay
            ? (long)ResolveDelayMs(
                fixedDelayMs: 0,
                hasRandomDelay: true,
                randomDelayMinMs: macro.TrailingDelayMinMs,
                randomDelayMaxMs: macro.TrailingDelayMaxMs) * MacroTiming.MicrosecondsPerMillisecond
            : 0;
        return checked(macro.TrailingDelayMicroseconds + randomDelayMicroseconds);
    }

    private async Task CacheResolutionAsync()
    {
        if (!_resolutionCached && _positionProvider is not null)
        {
            try
            {
                var bounds = await _positionProvider.GetDesktopBoundsAsync().ConfigureAwait(false);
                if (bounds is null)
                {
                    var resolution = await _positionProvider.GetScreenResolutionAsync().ConfigureAwait(false);
                    if (resolution is { Width: > 0, Height: > 0 })
                    {
                        bounds = new ScreenRect(0, 0, resolution.Value.Width, resolution.Value.Height);
                    }
                }

                if (bounds is not null)
                {
                    _cachedDesktopBounds = bounds;
                    _cachedScreenWidth = bounds.Value.Width;
                    _cachedScreenHeight = bounds.Value.Height;
                    _resolutionCached = true;
                    Log.Information(
                        "[MacroPlayer] Desktop bounds cached: ({X},{Y}) {Width}x{Height}",
                        bounds.Value.X,
                        bounds.Value.Y,
                        bounds.Value.Width,
                        bounds.Value.Height);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[MacroPlayer] Failed to get resolution");
            }
        }

        if (_positionProvider is not null && !_positionProvider.SupportsAbsolutePosition)
        {
            Log.Information(
                "[MacroPlayer] Position provider {ProviderName} is resolution-only for playback; absolute playback will require an absolute-capable input simulator",
                _positionProvider.ProviderName);
        }
        else if (_positionProvider is IMousePositionAvailability { IsPositionAvailable: false })
        {
            Log.Debug(
                "[MacroPlayer] Position provider {ProviderName} exposes absolute coordinates but has no current cursor sample; playback will use the configured input/coordinate fallback",
                _positionProvider.ProviderName);
        }

        Log.Information("[MacroPlayer] Using screen resolution: {Width}x{Height}",
            _cachedScreenWidth, _cachedScreenHeight);
    }

    private async Task AcquireSimulatorAsync(MacroSequence macro)
    {
        bool needsAbsoluteDevice = MacroPositionSemantics.HasAnyLogicalDesktopCoordinateEvents(macro)
            || MacroPositionSemantics.RequiresInitialCornerReset(macro)
            || HasLogicalDesktopRuntimeMoveSteps(macro)
            || HasImageClickRuntimeScriptSteps(macro);
        bool canCreateAbsoluteDevice = needsAbsoluteDevice && _resolutionCached;
        int deviceWidth = canCreateAbsoluteDevice ? _cachedScreenWidth : 0;
        int deviceHeight = canCreateAbsoluteDevice ? _cachedScreenHeight : 0;
        await _session.AcquireAsync(deviceWidth, deviceHeight, _session.Token).ConfigureAwait(false);
        _inputSimulator = _session.Simulator;
        Log.Information("[MacroPlayer] Acquired device: {ProviderName}", _inputSimulator!.ProviderName);
    }

    private void EnsureAbsolutePlaybackSupported(MacroSequence macro)
    {
        if (!MacroPositionSemantics.HasAnyLogicalDesktopCoordinateEvents(macro)
            && !HasLogicalDesktopRuntimeMoveSteps(macro))
        {
            return;
        }

        if (_inputSimulator is not IInputSimulatorCapabilities capabilities
            || !capabilities.SupportsAbsoluteCoordinates)
        {
            ThrowAbsolutePlaybackUnsupported();
        }
    }

    private void ThrowAbsolutePlaybackUnsupported()
    {
        throw new AbsolutePlaybackUnsupportedException(_inputSimulator!.ProviderName);
    }

    private async Task InitializePlaybackComponentsAsync(MacroSequence macro)
    {
        var buttonTracker = _buttonTrackerFactory();
        var keyTracker = _keyTrackerFactory();
        _coordinator = _coordinatorFactory();
        _coordinator.ConfigureDesktopBounds(_cachedDesktopBounds);

        // Create event executor with all dependencies
        _eventExecutor = new MacroEventExecutor(
            _inputSimulator!,
            buttonTracker,
            keyTracker,
            _buttonMapper,
            _coordinator);
        _session.AttachInputState(_eventExecutor, buttonTracker, keyTracker);

        _eventExecutor.Initialize(_cachedDesktopBounds);

        // Initialize coordinator for first iteration
        await _coordinator.InitializeAsync(macro, _inputSimulator!,
            _cachedScreenWidth, _cachedScreenHeight, _session.Token).ConfigureAwait(false);

        // Re-anchor the reusable virtual device before logical absolute input.
        if ((MacroPositionSemantics.HasAnyLogicalDesktopCoordinateEvents(macro)
                || HasLogicalDesktopRuntimeMoveSteps(macro))
            && _coordinator.HasKnownPosition)
        {
            _eventExecutor.MoveAbsolute(_coordinator.CurrentX, _coordinator.CurrentY);
            await EnsureAbsolutePositionSettledAsync(
                _coordinator,
                _coordinator.CurrentX,
                _coordinator.CurrentY,
                _session.Token).ConfigureAwait(false);
        }
    }

    private async Task PlayOnceAsync(
        MacroSequence macro,
        double speedMultiplier,
        PlaybackOptions options,
        CancellationToken cancellationToken)
    {
        bool useLegacyCurrentPositionInterpretation = MacroPositionSemantics.IsLegacyCurrentPositionMacro(macro);
        var state = new PlaybackRunState
        {
            ObservedPauseResumeVersion = _session.PauseResumeVersion,
        };
        var trajectoryPlan = MotionTrajectoryResampler.CreatePlan(macro, speedMultiplier, options);
        int totalEvents = trajectoryPlan.Events.Count;
        var playbackElapsedMilliseconds = _playbackElapsedMillisecondsFactory();

        if (trajectoryPlan.ResampledSegmentCount > 0)
        {
            Log.Information(
                "[MacroPlayer] Duration-first adaptive motion plan: OmittedSamples={OmittedSampleCount}, Segments={SegmentCount}, CurvatureAnchors={CurvatureAnchors}, MaximumGeometricErrorPx={MaximumGeometricErrorPx:F3}, ErrorBoundSatisfied={ErrorBoundSatisfied}",
                trajectoryPlan.OmittedSampleCount,
                trajectoryPlan.ResampledSegmentCount,
                trajectoryPlan.AdaptiveAnchorReplacementCount,
                trajectoryPlan.MaximumGeometricErrorPixels,
                trajectoryPlan.IsErrorBoundSatisfied);
        }

        if (!trajectoryPlan.IsErrorBoundSatisfied)
        {
            Log.Warning(
                "[MacroPlayer] Duration-first motion quality limit: RequestedSpeed={RequestedSpeed:F3}x, MaximumGeometricErrorPx={MaximumGeometricErrorPx:F3}, AllowedErrorPx={AllowedErrorPx:F3}. The selected report rate cannot meet the configured geometry budget; lower speed, raise the rate, or use Precision.",
                speedMultiplier,
                trajectoryPlan.MaximumGeometricErrorPixels,
                options.MaximumMotionErrorPixels);
        }

        Log.Debug("[MacroPlayer] Starting playback of {Total} events at {Speed}x speed", totalEvents, speedMultiplier);

        var playbackStarted = Stopwatch.GetTimestamp();
        try
        {
            await ExecutePlaybackEventsAsync(
                trajectoryPlan.Events,
                macro,
                speedMultiplier,
                playbackElapsedMilliseconds,
                state,
                useLegacyCurrentPositionInterpretation,
                totalEvents,
                cancellationToken).ConfigureAwait(false);

            await FlushPendingCursorMovementAsync(state, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(playbackStarted);
            LogMotionTransportSummary(
                trajectoryPlan.Events,
                macro,
                options,
                speedMultiplier,
                state,
                elapsed,
                trajectoryPlan);
        }

        Log.Debug("[MacroPlayer] Completed playback of {Total} events", totalEvents);
    }

    private async Task ExecutePlaybackEventsAsync(
        IList<MacroEvent> events,
        MacroSequence macro,
        double speedMultiplier,
        Func<double> playbackElapsedMilliseconds,
        PlaybackRunState state,
        bool useLegacyCurrentPositionInterpretation,
        int totalEvents,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < events.Count;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryCreateMotionTrajectorySlice(
                    events,
                    index,
                    macro,
                    speedMultiplier,
                    out var samples,
                    out var finalLogicalTarget)
                && samples.Count > 1)
            {
                await WaitForEventScheduleAsync(
                    events[index],
                    speedMultiplier,
                    playbackElapsedMilliseconds,
                    state,
                    cancellationToken).ConfigureAwait(false);

                var trajectorySimulator = (IAbsoluteMotionTrajectorySimulator)_inputSimulator!;
                await trajectorySimulator.SimulateAbsoluteTrajectoryAsync(samples, cancellationToken).ConfigureAwait(false);

                var executor = _eventExecutor ?? throw new InvalidOperationException("Playback event executor is not initialized.");
                executor.CompleteAbsoluteTrajectory(finalLogicalTarget.X, finalLogicalTarget.Y);
                state.EventCount += samples.Count - 1;
                state.TrajectorySampleCount += samples.Count;
                state.HasPendingLogicalMove = true;
                // Reuse the same bounds and endpoint rules for settlement checks.
                state.PendingLogicalX = _coordinator?.CurrentX ?? finalLogicalTarget.X;
                state.PendingLogicalY = _coordinator?.CurrentY ?? finalLogicalTarget.Y;

                // Re-anchor after daemon pacing; never flood the next slice.
                state.TimelineAnchorElapsedMs = playbackElapsedMilliseconds();
                state.ScheduledElapsedMs = 0;
                state.HasTimelineAnchor = true;

                index += samples.Count;
                continue;
            }

            await ExecutePlaybackEventAsync(
                macro,
                events[index],
                speedMultiplier,
                playbackElapsedMilliseconds,
                state,
                useLegacyCurrentPositionInterpretation,
                totalEvents,
                cancellationToken).ConfigureAwait(false);
            index++;
        }
    }

    private bool TryCreateMotionTrajectorySlice(
        IList<MacroEvent> events,
        int startIndex,
        MacroSequence macro,
        double speedMultiplier,
        out List<AbsoluteMotionTrajectorySample> samples,
        out (int X, int Y) finalLogicalTarget)
    {
        return TryCreateAbsoluteTrajectorySlice(
                   events,
                   startIndex,
                   macro,
                   speedMultiplier,
                   out samples,
                   out finalLogicalTarget)
            || TryCreateLogicalRelativeTrajectorySlice(
                events,
                startIndex,
                macro,
                speedMultiplier,
                out samples,
                out finalLogicalTarget);
    }

    private bool TryCreateAbsoluteTrajectorySlice(
        IList<MacroEvent> events,
        int startIndex,
        MacroSequence macro,
        double speedMultiplier,
        out List<AbsoluteMotionTrajectorySample> samples,
        out (int X, int Y) finalLogicalTarget)
    {
        samples = [];
        finalLogicalTarget = default;

        if (_inputSimulator is not IAbsoluteMotionTrajectorySimulator
            || _eventExecutor is not { IsMouseButtonPressed: true }
            || startIndex >= events.Count)
        {
            return false;
        }

        long totalDelayMicroseconds = 0;
        for (var index = startIndex; index < events.Count && samples.Count < MaxDaemonTrajectorySamples; index++)
        {
            var ev = events[index];
            if (ev.Type is not EventType.MouseMove
                || ev.HasRandomDelay
                || MacroPositionSemantics.ResolveCoordinateMode(ev, macro.IsAbsoluteCoordinates) is not MouseCoordinateMode.Absolute
                || MacroPositionSemantics.ResolveCoordinateSpace(ev, macro.IsAbsoluteCoordinates) is not MouseCoordinateSpace.LogicalDesktop)
            {
                break;
            }

            if (!TryAppendTrajectoryDelay(
                    samples,
                    ev.DelayMicroseconds,
                    speedMultiplier,
                    ref totalDelayMicroseconds))
            {
                break;
            }

            var executor = _eventExecutor ?? throw new InvalidOperationException("Playback event executor is not initialized.");
            samples.Add(executor.CreateAbsoluteTrajectorySample(ev.X, ev.Y, delayAfterMicroseconds: 0));
            finalLogicalTarget = (ev.X, ev.Y);
        }

        return samples.Count > 1;
    }

    private bool TryCreateLogicalRelativeTrajectorySlice(
        IList<MacroEvent> events,
        int startIndex,
        MacroSequence macro,
        double speedMultiplier,
        out List<AbsoluteMotionTrajectorySample> samples,
        out (int X, int Y) finalLogicalTarget)
    {
        samples = [];
        finalLogicalTarget = default;

        var coordinator = _coordinator;
        var executor = _eventExecutor;
        if (_inputSimulator is not IAbsoluteMotionTrajectorySimulator
            || coordinator is not { HasKnownPosition: true }
            || executor is null
            || startIndex >= events.Count)
        {
            return false;
        }

        (int X, int Y) target = (coordinator.CurrentX, coordinator.CurrentY);
        long totalDelayMicroseconds = 0;
        for (var index = startIndex; index < events.Count && samples.Count < MaxDaemonTrajectorySamples; index++)
        {
            var ev = events[index];
            if (ev.Type is not EventType.MouseMove
                || ev.HasRandomDelay
                || MacroPositionSemantics.ResolveCoordinateMode(ev, macro.IsAbsoluteCoordinates) is not MouseCoordinateMode.Relative
                || MacroPositionSemantics.ResolveCoordinateSpace(ev, macro.IsAbsoluteCoordinates) is not MouseCoordinateSpace.LogicalDesktop)
            {
                break;
            }

            if (!TryAppendTrajectoryDelay(
                    samples,
                    ev.DelayMicroseconds,
                    speedMultiplier,
                    ref totalDelayMicroseconds))
            {
                break;
            }

            target = executor.ClampLogicalTarget((long)target.X + ev.X, (long)target.Y + ev.Y);
            samples.Add(executor.CreateAbsoluteTrajectorySample(target.X, target.Y, delayAfterMicroseconds: 0));
            finalLogicalTarget = target;
        }

        return samples.Count > 1;
    }

    private static bool TryAppendTrajectoryDelay(
        List<AbsoluteMotionTrajectorySample> samples,
        long sourceDelayMicroseconds,
        double speedMultiplier,
        ref long totalDelayMicroseconds)
    {
        if (samples.Count is 0)
        {
            return true;
        }

        long delayFromPreviousMicroseconds = ScaleDelayMicroseconds(sourceDelayMicroseconds, speedMultiplier);
        if (totalDelayMicroseconds + delayFromPreviousMicroseconds > MaxDaemonTrajectoryDurationMicroseconds)
        {
            return false;
        }

        var previousSample = samples[^1];
        samples[^1] = previousSample with { DelayAfterMicroseconds = delayFromPreviousMicroseconds };
        totalDelayMicroseconds += delayFromPreviousMicroseconds;
        return true;
    }

    private static long ScaleDelayMicroseconds(long sourceDelayMicroseconds, double speedMultiplier)
    {
        if (sourceDelayMicroseconds <= 0)
        {
            return 0;
        }

        return Math.Max(0, Convert.ToInt64(Math.Round(
            sourceDelayMicroseconds / speedMultiplier,
            MidpointRounding.AwayFromZero)));
    }

    private async Task PlayOnceRuntimeScriptAsync(
        MacroSequence macro,
        double speedMultiplier,
        PlaybackOptions options,
        CancellationToken cancellationToken)
    {
        bool useLegacyCurrentPositionInterpretation = MacroPositionSemantics.IsLegacyCurrentPositionMacro(macro);
        var state = new PlaybackRunState
        {
            ObservedPauseResumeVersion = _session.PauseResumeVersion,
        };
        var playbackElapsedMilliseconds = _playbackElapsedMillisecondsFactory();
        var screenReadExecutor = new RunScriptScreenReadExecutor(
            _screenPixelReader,
            _positionProvider,
            executeEventAsync: (ev, token) => ExecutePlaybackEventAsync(
                macro,
                ev,
                speedMultiplier,
                playbackElapsedMilliseconds,
                state,
                useLegacyCurrentPositionInterpretation,
                macro.Events.Count,
                token),
            imageClickMovementResolver: _imageClickMovementResolver,
            inputSimulator: _inputSimulator,
            imageAssetCodec: _imageAssetCodec,
            flushPendingCursorMovementAsync: token => FlushPendingCursorMovementAsync(state, token));
        var windowExecutor = new RunScriptWindowExecutor(_windowManager);
        var clipboardExecutor = new RunScriptClipboardExecutor(_clipboardService);
        var shellExecutor = new RunScriptShellExecutor(_shellCommandRunner, _timingService, this);
        var screenshotExecutor = new RunScriptScreenshotExecutor(_screenshotCaptureService);
        var runtimeExecutor = new RunScriptRuntimeExecutor(
            _keyCodeMapper,
            _timingService,
            this,
            _runtimeVariables,
            screenReadExecutor,
            windowExecutor,
            clipboardExecutor,
            shellExecutor,
            screenshotExecutor);
        var executionRequest = new RunScriptRuntimeExecutionRequest(
            macro.ScriptSteps,
            macro.Images,
            speedMultiplier,
            (ev, token) => ExecutePlaybackEventAsync(
                macro,
                ev,
                speedMultiplier,
                playbackElapsedMilliseconds,
                state,
                useLegacyCurrentPositionInterpretation,
                macro.Events.Count,
                token),
            ResolveDelayMs);

        var runtimeCoordinator = new RunScriptRuntimeCoordinator(runtimeExecutor);
        await runtimeCoordinator.ExecuteAsync(executionRequest, cancellationToken).ConfigureAwait(false);
        await FlushPendingCursorMovementAsync(state, cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForEventScheduleAsync(
        MacroEvent ev,
        double speedMultiplier,
        Func<double> playbackElapsedMilliseconds,
        PlaybackRunState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_session.IsPaused)
        {
            Log.Debug("[MacroPlayer] Paused before event {Current}", state.EventCount);
            var pausedStartMs = playbackElapsedMilliseconds();
            await _session.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
            var pausedDurationMs = playbackElapsedMilliseconds() - pausedStartMs;
            if (state.HasTimelineAnchor)
            {
                state.TimelineAnchorElapsedMs += pausedDurationMs;
            }
        }

        int currentPauseResumeVersion = _session.PauseResumeVersion;
        if (currentPauseResumeVersion != state.ObservedPauseResumeVersion)
        {
            state.ObservedPauseResumeVersion = currentPauseResumeVersion;
            if (state.HasTimelineAnchor)
            {
                state.ScheduledElapsedMs = playbackElapsedMilliseconds() - state.TimelineAnchorElapsedMs;
            }
        }

        state.EventCount++;

        double eventDelaySource = ResolveEventDelayMilliseconds(ev);
        bool isMotionEvent = ev.Type is EventType.MouseMove;
        if (eventDelaySource <= 0)
        {
            return;
        }

        double adjustedDelay = eventDelaySource / speedMultiplier;
        if (!state.HasTimelineAnchor)
        {
            state.TimelineAnchorElapsedMs = playbackElapsedMilliseconds();
            state.HasTimelineAnchor = true;
        }

        state.ScheduledElapsedMs += adjustedDelay;
        var elapsedSinceAnchorMs = playbackElapsedMilliseconds() - state.TimelineAnchorElapsedMs;
        var remainingDelayMs = state.ScheduledElapsedMs - elapsedSinceAnchorMs;
        if (remainingDelayMs > 0)
        {
            await _timingService.WaitAsync(remainingDelayMs, this, cancellationToken).ConfigureAwait(false);
            elapsedSinceAnchorMs = playbackElapsedMilliseconds() - state.TimelineAnchorElapsedMs;
            remainingDelayMs = state.ScheduledElapsedMs - elapsedSinceAnchorMs;
        }

        if (remainingDelayMs <= 0
            && (isMotionEvent || ShouldResetPlaybackTimeline(remainingDelayMs, adjustedDelay)))
        {
            state.ScheduledElapsedMs = elapsedSinceAnchorMs;
        }
    }

    private async Task ExecutePlaybackEventAsync(
        MacroSequence macro,
        MacroEvent ev,
        double speedMultiplier,
        Func<double> playbackElapsedMilliseconds,
        PlaybackRunState state,
        bool useLegacyCurrentPositionInterpretation,
        int totalEvents,
        CancellationToken cancellationToken)
    {
        await WaitForEventScheduleAsync(
            ev,
            speedMultiplier,
            playbackElapsedMilliseconds,
            state,
            cancellationToken).ConfigureAwait(false);

        try
        {
            Log.Debug("[MacroPlayer] Executing {Current}/{Total}: {Type} | X={X} Y={Y} | Key={Key} Button={Button}",
                state.EventCount, totalEvents, ev.Type, ev.X, ev.Y, ev.KeyCode, ev.Button);

            bool usesCurrentPosition = MacroPositionSemantics.UsesCurrentPosition(ev, useLegacyCurrentPositionInterpretation);
            var eventToExecute = ev;
            if (usesCurrentPosition)
            {
                eventToExecute.UseCurrentPosition = true;
                eventToExecute.X = 0;
                eventToExecute.Y = 0;
            }

            var coordinateMode = MacroPositionSemantics.ResolveCoordinateMode(eventToExecute, macro.IsAbsoluteCoordinates);
            var coordinateSpace = MacroPositionSemantics.ResolveCoordinateSpace(eventToExecute, macro.IsAbsoluteCoordinates);
            if (coordinateMode is MouseCoordinateMode.Relative
                && coordinateSpace is MouseCoordinateSpace.LogicalDesktop
                && !_coordinator!.HasKnownPosition)
            {
                _ = await _coordinator.TrySynchronizePositionAsync(cancellationToken).ConfigureAwait(false);
            }

            var coordinator = _coordinator ?? throw new InvalidOperationException("Playback coordinator is not initialized.");
            var executor = _eventExecutor ?? throw new InvalidOperationException("Playback event executor is not initialized.");
            bool isNonScrollMouseButtonEvent = MacroPositionSemantics.IsNonScrollMouseButtonEvent(eventToExecute);
            bool isScrollMouseButtonEvent = IsScrollMouseButtonEvent(eventToExecute);
            bool willResolveExplicitAbsoluteButtonPosition = coordinateMode is MouseCoordinateMode.Absolute
                && isNonScrollMouseButtonEvent
                && !eventToExecute.UseCurrentPosition;
            bool willResolveLogicalRelativeButtonPosition = coordinateMode is MouseCoordinateMode.Relative
                && coordinateSpace is MouseCoordinateSpace.LogicalDesktop
                && isNonScrollMouseButtonEvent
                && !eventToExecute.UseCurrentPosition
                && (eventToExecute.X is not 0 || eventToExecute.Y is not 0);

            bool shouldFlushPendingCursorMovement = !willResolveExplicitAbsoluteButtonPosition
                && (isNonScrollMouseButtonEvent
                    || isScrollMouseButtonEvent
                    || (coordinateMode is MouseCoordinateMode.Relative
                        && coordinateSpace is MouseCoordinateSpace.RawDevice));
            if (shouldFlushPendingCursorMovement)
            {
                await FlushPendingCursorMovementAsync(state, cancellationToken).ConfigureAwait(false);
            }

            if (willResolveExplicitAbsoluteButtonPosition)
            {
                executor.MoveAbsolute(eventToExecute.X, eventToExecute.Y);
                await EnsureAbsolutePositionSettledAsync(
                    coordinator,
                    coordinator.CurrentX,
                    coordinator.CurrentY,
                    cancellationToken).ConfigureAwait(false);
                state.HasPendingLogicalMove = false;
                eventToExecute.UseCurrentPosition = true;
                eventToExecute.X = 0;
                eventToExecute.Y = 0;
            }
            else if (willResolveLogicalRelativeButtonPosition)
            {
                if (!coordinator.HasKnownPosition)
                {
                    throw new LogicalRelativePositionUnavailableException();
                }

                var target = executor.ClampLogicalTarget(
                    (long)coordinator.CurrentX + eventToExecute.X,
                    (long)coordinator.CurrentY + eventToExecute.Y);
                executor.MoveAbsolute(target.X, target.Y);
                await EnsureAbsolutePositionSettledAsync(
                    coordinator,
                    target.X,
                    target.Y,
                    cancellationToken).ConfigureAwait(false);
                state.HasPendingLogicalMove = false;
                eventToExecute.X = 0;
                eventToExecute.Y = 0;
            }

            executor.Execute(eventToExecute, coordinateMode, coordinateSpace);
            // Do not serialize high-frequency movement on compositor feedback.
            // Defer the safety check to the next pointer-dependent operation.
            if (eventToExecute.Type is EventType.MouseMove)
            {
                if (coordinateSpace is MouseCoordinateSpace.LogicalDesktop
                    && coordinateMode is MouseCoordinateMode.Absolute or MouseCoordinateMode.Relative)
                {
                    state.DirectLogicalMoveCount++;
                }

                if (coordinateSpace is MouseCoordinateSpace.LogicalDesktop && coordinator.HasKnownPosition)
                {
                    state.HasPendingLogicalMove = true;
                    state.PendingLogicalX = coordinator.CurrentX;
                    state.PendingLogicalY = coordinator.CurrentY;
                }
                else
                {
                    state.HasPendingLogicalMove = false;
                }
            }
        }
        catch (AbsolutePlaybackUnsupportedException)
        {
            throw;
        }
        catch (LogicalRelativePositionUnavailableException)
        {
            throw;
        }
        catch (InputInjectionPermissionRequiredException)
        {
            throw;
        }
        catch (InputInjectionFailedException)
        {
            throw;
        }
        catch (ImageClickMovementUnsupportedException)
        {
            throw;
        }
        catch (AbsoluteCursorMoveNotSettledException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[MacroPlayer] Error executing event {Current}/{Total}: {Type}", state.EventCount, totalEvents, ev.Type);
            if (++_errorCount > MaxPlaybackErrors)
            {
                Log.Fatal("[MacroPlayer] Too many errors ({Count}), aborting", _errorCount);
                throw new InvalidOperationException($"Playback aborted after {_errorCount.ToString(CultureInfo.InvariantCulture)} errors", ex);
            }
        }

    }

    private async Task FlushPendingCursorMovementAsync(
        PlaybackRunState state,
        CancellationToken cancellationToken)
    {
        var coordinator = _coordinator;
        if (coordinator is null)
        {
            return;
        }

        if (state.HasPendingLogicalMove)
        {
            await EnsureAbsolutePositionSettledAsync(
                coordinator,
                state.PendingLogicalX,
                state.PendingLogicalY,
                cancellationToken).ConfigureAwait(false);
            state.HasPendingLogicalMove = false;
        }

        if (!coordinator.HasKnownPosition)
        {
            _ = await coordinator.TrySynchronizePositionAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void LogMotionTransportSummary(
        IList<MacroEvent> events,
        MacroSequence macro,
        PlaybackOptions options,
        double speedMultiplier,
        PlaybackRunState state,
        TimeSpan elapsed,
        MotionTrajectoryResampler.Plan trajectoryPlan)
    {
        int inputLogicalMoveCount = events.Count(ev => ev.Type is EventType.MouseMove
            && MacroPositionSemantics.ResolveCoordinateMode(ev, macro.IsAbsoluteCoordinates) is MouseCoordinateMode.Absolute or MouseCoordinateMode.Relative
            && MacroPositionSemantics.ResolveCoordinateSpace(ev, macro.IsAbsoluteCoordinates) is MouseCoordinateSpace.LogicalDesktop);
        if (inputLogicalMoveCount is 0)
        {
            return;
        }

        Log.Information(
            "[MacroPlayer] Motion playback completed: Mode={Mode}, EffectiveSpeed={EffectiveSpeed:F3}x, LogicalMoves={LogicalMoves}, TrajectorySamples={TrajectorySamples}, DirectLogicalMoves={DirectLogicalMoves}, ResampledSegments={ResampledSegments}, OmittedSamples={OmittedSamples}, MaximumGeometricErrorPx={MaximumGeometricErrorPx:F3}, ErrorBoundSatisfied={ErrorBoundSatisfied}, PlaybackElapsedMs={PlaybackElapsedMs:F3}",
            options.MotionMode,
            speedMultiplier,
            inputLogicalMoveCount,
            state.TrajectorySampleCount,
            state.DirectLogicalMoveCount,
            trajectoryPlan.ResampledSegmentCount,
            trajectoryPlan.OmittedSampleCount,
            trajectoryPlan.MaximumGeometricErrorPixels,
            trajectoryPlan.IsErrorBoundSatisfied,
            elapsed.TotalMilliseconds);
    }

    private static bool IsScrollMouseButtonEvent(MacroEvent ev)
    {
        return ev.Type is EventType.ButtonPress or EventType.ButtonRelease or EventType.Click
            && MacroPositionSemantics.IsScrollButton(ev.Button);
    }

    private static async Task EnsureAbsolutePositionSettledAsync(
        IPlaybackCoordinator coordinator,
        int expectedX,
        int expectedY,
        CancellationToken cancellationToken)
    {
        var settled = await coordinator.WaitForPositionAsync(
            expectedX,
            expectedY,
            cancellationToken).ConfigureAwait(false);
        if (!settled)
        {
            throw new AbsoluteCursorMoveNotSettledException(expectedX, expectedY);
        }
    }

    private async Task ExecuteScreenReadScriptStepsAsync(MacroSequence macro, CancellationToken cancellationToken)
    {
        if (macro.ScriptSteps.Count is 0 || !HasRuntimeScriptSteps(macro))
        {
            return;
        }

        if (_screenPixelReader is null)
        {
            throw new InvalidOperationException("Screen-reading script steps require an IScreenPixelReader runtime service.");
        }

        var executor = new RunScriptScreenReadExecutor(_screenPixelReader, _positionProvider, imageClickMovementResolver: _imageClickMovementResolver, inputSimulator: _inputSimulator, imageAssetCodec: _imageAssetCodec);
        await executor.ExecuteAsync(macro, _runtimeVariables, cancellationToken).ConfigureAwait(false);
    }

    private async Task SetupRuntimeScriptOnlyAsync(
        MacroSequence macro,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_screenPixelReader is null && HasScreenReadingScriptSteps(macro))
        {
            throw new InvalidOperationException("Screen-reading script steps require an IScreenPixelReader runtime service.");
        }

        if (HasRuntimeInputScriptSteps(macro))
        {
            await CacheResolutionAsync().ConfigureAwait(false);
            await AcquireSimulatorAsync(macro).ConfigureAwait(false);
            EnsureAbsolutePlaybackSupported(macro);
            await InitializePlaybackComponentsAsync(macro).ConfigureAwait(false);
        }
    }

    private static bool HasScreenReadingScriptSteps(MacroSequence macro)
    {
        return macro.ScriptSteps.Any(RunScriptScreenReadExecutor.IsScreenReadingStep);
    }

    private static bool HasRuntimeScriptSteps(MacroSequence macro)
    {
        return macro.ScriptSteps.Any(IsRuntimeScriptStep);
    }

    private static bool HasOnlyRuntimeScriptSteps(MacroSequence macro)
    {
        return macro.ScriptSteps.Count > 0
            && macro.ScriptSteps
                .Where(step => !string.IsNullOrWhiteSpace(step))
                .All(IsRuntimeScriptStep);
    }

    private static bool IsRuntimeScriptStep(string step)
    {
        return RunScriptRuntimeStepClassifier.IsRuntimeStep(step);
    }

    private static bool HasLogicalDesktopRuntimeMoveSteps(MacroSequence macro)
    {
        return macro.ScriptSteps.Any(step =>
        {
            var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length >= 2
                && string.Equals(parts[0], "move", StringComparison.OrdinalIgnoreCase)
                && RunScriptSyntax.TryParseMouseMoveMode(parts[1], out _, out var coordinateSpace)
                && coordinateSpace is MouseCoordinateSpace.LogicalDesktop;
        });
    }

    private static bool HasImageClickRuntimeScriptSteps(MacroSequence macro)
    {
        return macro.ScriptSteps.Any(step =>
            step.TrimStart().StartsWith("imageclick ", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasRuntimeInputScriptSteps(MacroSequence macro)
    {
        return macro.ScriptSteps.Any(step =>
        {
            var trimmed = step.TrimStart();
            return trimmed.StartsWith("imageclick ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("move ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("click ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("down ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("up ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("scroll ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("tap ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("type ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("key ", StringComparison.OrdinalIgnoreCase);
        });
    }

    private int ResolveRepeatDelayMs(PlaybackOptions options)
    {
        if (options.UseRandomRepeatDelay)
        {
            return ResolveDelayMs(0, hasRandomDelay: true, options.RepeatDelayMinMs, options.RepeatDelayMaxMs);
        }

        return Math.Max(0, options.RepeatDelayMs);
    }

    private static bool ShouldResetPlaybackTimeline(double remainingDelayMs, double adjustedDelayMs)
    {
        double allowedDriftMs = Math.Max(MinCatchUpResetDriftMs, adjustedDelayMs * CatchUpResetDelayMultiplier);
        return remainingDelayMs <= -allowedDriftMs;
    }

    private int ResolveDelayMs(int fixedDelayMs, bool hasRandomDelay, int randomDelayMinMs, int randomDelayMaxMs)
    {
        return _delayResolver.Resolve(fixedDelayMs, hasRandomDelay, randomDelayMinMs, randomDelayMaxMs);
    }

    private double ResolveEventDelayMilliseconds(MacroEvent ev)
    {
        double fixedDelayMilliseconds = ev.DelayMicroseconds
            / (double)MacroTiming.MicrosecondsPerMillisecond;
        if (ev.HasRandomDelay)
        {
            return fixedDelayMilliseconds + ResolveDelayMs(
                fixedDelayMs: 0,
                hasRandomDelay: true,
                randomDelayMinMs: ev.RandomDelayMinMs,
                randomDelayMaxMs: ev.RandomDelayMaxMs);
        }

        return fixedDelayMilliseconds;
    }

    public void Pause()
    {
        if (IsPlaying && !_session.IsPaused)
        {
            _session.Pause();

            Log.Information("[MacroPlayer] Paused at loop {Loop}/{Total} (saved {ButtonCount} buttons, {KeyCount} keys)",
                CurrentLoop, TotalLoops, 0, 0);
        }
    }

    public void ResumePlayback()
    {
        if (IsPlaying && _session.IsPaused)
        {
            _session.ResumePlayback();
            Log.Information("[MacroPlayer] Resumed");
        }
    }

    public void StopPlayback()
    {
        Log.Information("[MacroPlayer] Stop requested");
        _session.StopPlayback();
    }

    private async Task CleanupAsync()
    {
        IsPlaying = false;
        CurrentLoop = 0;
        TotalLoops = 0;
        IsWaitingBetweenLoops = false;

        await _session.StopPlaybackAsync().ConfigureAwait(false);
        _session.End();
        _eventExecutor?.Dispose();
        _eventExecutor = null;
        _coordinator = null;
        Log.Information("[MacroPlayer] ========== PLAYBACK ENDED ==========");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        StopPlayback();
        _session.AttachInputState(executor: null, buttons: null, keys: null);
        _eventExecutor?.Dispose();
        _session.Dispose();

        GC.SuppressFinalize(this);
    }
}
