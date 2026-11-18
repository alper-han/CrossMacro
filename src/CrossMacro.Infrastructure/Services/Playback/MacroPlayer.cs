
namespace CrossMacro.Infrastructure.Services.Playback;


/// <summary>Plays macros: owns the playback loop, pause/resume/stop and the lifetime of the simulator for one run.</summary>
public sealed partial class MacroPlayer : IMacroPlayer, IAsyncDisposable, IRunScriptRuntimeVariableSource
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
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly PlaybackDelayResolver _delayResolver = new();
    private readonly PlaybackSessionResourceOwner _session;
    private PlaybackRequirements _requirements = PlaybackRequirements.Empty;

    private IInputSimulator? _inputSimulator;
    private MacroEventExecutor? _eventExecutor;
    private IPlaybackCoordinator? _coordinator;

    private int _disposed;
    private volatile TaskCompletionSource? _runCompleted;

    private int _cachedScreenWidth;
    private int _cachedScreenHeight;
    private ScreenRect? _cachedDesktopBounds;
    private bool _resolutionCached;

    private readonly IDictionary<string, string> _runtimeVariables;

    private static readonly TimeSpan DisposeWaitTimeout = TimeSpan.FromSeconds(5);

    private sealed class PlaybackRunState(PlaybackTimeline timeline)
    {
        public PlaybackTimeline Timeline { get; } = timeline;
        // Logical desktop motion may be applied asynchronously; verify it before pointer-dependent work.
        public bool HasPendingLogicalMove;
        public int PendingLogicalX;
        public int PendingLogicalY;
        public bool PendingLogicalMoveIsRelative;
        public bool AllowsCooperativeLogicalRelativeMovement;
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
        _keyCodeMapper = dependencies.KeyCodeMapper;
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

    public async Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(macro);
        ObjectDisposedException.ThrowIf(_disposed is not 0, this);

        await RunLoopAsync(macro, options ?? new PlaybackOptions(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Validates the macro and applies the precision rate plan; returns the speed playback runs at.</summary>
    private double PrepareRun(MacroSequence macro, PlaybackOptions options)
    {
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

        options.Normalize();
        var precisionRatePlan = PrecisionMotionRateGovernor.CreatePlan(macro, options);

        if (precisionRatePlan.IsQualityLimited)
        {
            Log.Information(
                "[MacroPlayer] Precision quality lock: RequestedSpeed={RequestedSpeed:F3}x, EffectiveSpeed={EffectiveSpeed:F3}x, SourcePeakReportsPerSecond={SourcePeakReportsPerSecond:F1}, CapReportsPerSecond={CapReportsPerSecond}",
                precisionRatePlan.RequestedSpeedMultiplier,
                precisionRatePlan.EffectiveSpeedMultiplier,
                precisionRatePlan.SourcePeakEventsPerSecond,
                precisionRatePlan.OutputCapEventsPerSecond);
        }

        return precisionRatePlan.EffectiveSpeedMultiplier;
    }

    private void BeginPlayback(CancellationToken cancellationToken)
    {
        _session.Begin(cancellationToken);
        _runCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _inputSimulator = null;
        _cachedDesktopBounds = null;
        _cachedScreenWidth = 0;
        _cachedScreenHeight = 0;
        _resolutionCached = false;
        IsPlaying = true;
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
        await AcquireSimulatorAsync().ConfigureAwait(false);
        EnsureAbsolutePlaybackSupported();
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

    private async Task AcquireSimulatorAsync()
    {
        bool canCreateAbsoluteDevice = _requirements.RequiresAbsoluteDevice && _resolutionCached;
        int deviceWidth = canCreateAbsoluteDevice ? _cachedScreenWidth : 0;
        int deviceHeight = canCreateAbsoluteDevice ? _cachedScreenHeight : 0;
        await _session.AcquireAsync(deviceWidth, deviceHeight, _session.Token).ConfigureAwait(false);
        _inputSimulator = _session.Simulator;
        Log.Information("[MacroPlayer] Acquired device: {ProviderName}", _inputSimulator!.ProviderName);
    }

    private void EnsureAbsolutePlaybackSupported()
    {
        if (!_requirements.RequiresAbsoluteCoordinates)
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
        var buttonTracker = new ButtonStateTracker();
        var keyTracker = new KeyStateTracker();
        _coordinator = new DefaultPlaybackCoordinator(_positionProvider);
        _coordinator.ConfigureDesktopBounds(_cachedDesktopBounds);

        // Create event executor with all dependencies
        _eventExecutor = new MacroEventExecutor(
            _inputSimulator!,
            buttonTracker,
            keyTracker,
            new DefaultPlaybackMouseButtonMapper(),
            _coordinator);
        _session.AttachInputState(_eventExecutor, buttonTracker, keyTracker);

        _eventExecutor.Initialize(_cachedDesktopBounds);

        // Initialize coordinator for first iteration
        await _coordinator.InitializeAsync(macro, _inputSimulator!,
            _cachedScreenWidth, _cachedScreenHeight, _session.Token).ConfigureAwait(false);

        // Re-anchor the reusable virtual device before logical absolute input.
        if (_requirements.RequiresInitialReanchor && _coordinator.HasKnownPosition)
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
        var state = new PlaybackRunState(new PlaybackTimeline(_session, _timingService, ResolveEventDelayMilliseconds))
        {
            AllowsCooperativeLogicalRelativeMovement = _requirements.AllowsCooperativeLogicalRelativeMovement,
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
            if (MotionBatchPlanner.TryCreate(
                new MotionPlanningContext(_inputSimulator, _eventExecutor, _coordinator),
                events,
                index,
                macro,
                speedMultiplier,
                state.AllowsCooperativeLogicalRelativeMovement,
                out var samples,
                out var finalLogicalTarget))
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
                state.Timeline.EventCount += samples.Count - 1;
                state.TrajectorySampleCount += samples.Count;
                state.HasPendingLogicalMove = true;
                state.PendingLogicalMoveIsRelative = MacroPositionSemantics.ResolveCoordinateMode(
                    events[index],
                    macro.IsAbsoluteCoordinates) is MouseCoordinateMode.Relative;
                // Reuse the same bounds and endpoint rules for settlement checks.
                state.PendingLogicalX = _coordinator?.CurrentX ?? finalLogicalTarget.X;
                state.PendingLogicalY = _coordinator?.CurrentY ?? finalLogicalTarget.Y;

                // Re-anchor after daemon pacing; never flood the next slice.
                state.Timeline.Reanchor(playbackElapsedMilliseconds());

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

    private async Task PlayOnceRuntimeScriptAsync(
        MacroSequence macro,
        double speedMultiplier,
        CancellationToken cancellationToken)
    {
        bool useLegacyCurrentPositionInterpretation = MacroPositionSemantics.IsLegacyCurrentPositionMacro(macro);
        var state = new PlaybackRunState(new PlaybackTimeline(_session, _timingService, ResolveEventDelayMilliseconds));
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
        var shellExecutor = new RunScriptShellExecutor(_shellCommandRunner, _timingService, _session);
        var screenshotExecutor = new RunScriptScreenshotExecutor(_screenshotCaptureService);
        var runtimeExecutor = new RunScriptRuntimeExecutor(
            _keyCodeMapper,
            _timingService,
            _session,
            _runtimeVariables,
            screenReadExecutor,
            windowExecutor,
            clipboardExecutor,
            shellExecutor,
            screenshotExecutor,
            new RunScriptMousePositionExecutor(_positionProvider));
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

        await runtimeExecutor.ExecuteAsync(executionRequest, cancellationToken).ConfigureAwait(false);
        await FlushPendingCursorMovementAsync(state, cancellationToken).ConfigureAwait(false);
    }

    private static Task WaitForEventScheduleAsync(
        MacroEvent ev,
        double speedMultiplier,
        Func<double> playbackElapsedMilliseconds,
        PlaybackRunState state,
        CancellationToken cancellationToken) =>
        state.Timeline.WaitAsync(ev, speedMultiplier, playbackElapsedMilliseconds, cancellationToken);

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
            if (Log.IsEnabled(CoreLogLevel.Debug))
            {
                Log.Debug("[MacroPlayer] Executing {Current}/{Total}: {Type} | X={X} Y={Y} | Key={Key} Button={Button}",
                    state.Timeline.EventCount, totalEvents, ev.Type, ev.X, ev.Y, ev.KeyCode, ev.Button);
            }

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
            var coordinator = _coordinator ?? throw new InvalidOperationException("Playback coordinator is not initialized.");
            if (state.AllowsCooperativeLogicalRelativeMovement
                && !state.HasPendingLogicalMove
                && eventToExecute.Type is EventType.MouseMove
                && coordinateMode is MouseCoordinateMode.Relative
                && coordinateSpace is MouseCoordinateSpace.LogicalDesktop)
            {
                _ = await coordinator.RefreshPositionAsync(cancellationToken).ConfigureAwait(false);
            }

            if (coordinateMode is MouseCoordinateMode.Relative
                && coordinateSpace is MouseCoordinateSpace.LogicalDesktop
                && !coordinator.HasKnownPosition)
            {
                _ = await coordinator.TrySynchronizePositionAsync(cancellationToken).ConfigureAwait(false);
            }

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

            bool shouldFlushPendingCursorMovement = state.HasPendingLogicalMove
                && !willResolveExplicitAbsoluteButtonPosition
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
                state.PendingLogicalMoveIsRelative = false;
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
                state.PendingLogicalMoveIsRelative = false;
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
                    state.PendingLogicalMoveIsRelative = coordinateMode is MouseCoordinateMode.Relative;
                }
                else
                {
                    state.HasPendingLogicalMove = false;
                    state.PendingLogicalMoveIsRelative = false;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.LogError(ex, "[MacroPlayer] Error executing event {Current}/{Total}: {Type}", state.Timeline.EventCount, totalEvents, ev.Type);
            throw;
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
            if (state.AllowsCooperativeLogicalRelativeMovement && state.PendingLogicalMoveIsRelative)
            {
                var expectedPosition = (X: state.PendingLogicalX, Y: state.PendingLogicalY);
                if (await coordinator.RefreshPositionAsync(cancellationToken).ConfigureAwait(false)
                    && (coordinator.CurrentX, coordinator.CurrentY) != expectedPosition)
                {
                    Log.Warning(
                        "[MacroPlayer] Logical relative cursor position changed from ({ExpectedX},{ExpectedY}) to ({ObservedX},{ObservedY}); continuing from the observed position.",
                        expectedPosition.X,
                        expectedPosition.Y,
                        coordinator.CurrentX,
                        coordinator.CurrentY);
                }
            }
            else
            {
                await EnsureAbsolutePositionSettledAsync(
                    coordinator,
                    state.PendingLogicalX,
                    state.PendingLogicalY,
                    cancellationToken).ConfigureAwait(false);
            }

            state.HasPendingLogicalMove = false;
            state.PendingLogicalMoveIsRelative = false;
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
        int inputLogicalMoveCount = 0;
        for (int index = 0; index < events.Count; index++)
        {
            var ev = events[index];
            if (ev.Type is EventType.MouseMove
                && MacroPositionSemantics.ResolveCoordinateMode(ev, macro.IsAbsoluteCoordinates) is MouseCoordinateMode.Absolute or MouseCoordinateMode.Relative
                && MacroPositionSemantics.ResolveCoordinateSpace(ev, macro.IsAbsoluteCoordinates) is MouseCoordinateSpace.LogicalDesktop)
            {
                inputLogicalMoveCount++;
            }
        }

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
            Log.Warning(
                "[MacroPlayer] Absolute cursor move did not settle at ({ExpectedX},{ExpectedY}); continuing playback.",
                expectedX,
                expectedY);
        }
    }

    private async Task SetupRuntimeScriptOnlyAsync(
        MacroSequence macro,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_screenPixelReader is null && _requirements.HasScreenReadingSteps)
        {
            throw new InvalidOperationException("Screen-reading script steps require an IScreenPixelReader runtime service.");
        }

        if (_requirements.RequiresRuntimeInput)
        {
            await CacheResolutionAsync().ConfigureAwait(false);
            await AcquireSimulatorAsync().ConfigureAwait(false);
            EnsureAbsolutePlaybackSupported();
            await InitializePlaybackComponentsAsync(macro).ConfigureAwait(false);
        }
    }

    private int ResolveRepeatDelayMs(PlaybackOptions options)
    {
        if (options.UseRandomRepeatDelay)
        {
            return ResolveDelayMs(0, hasRandomDelay: true, options.RepeatDelayMinMs, options.RepeatDelayMaxMs);
        }

        return Math.Max(0, options.RepeatDelayMs);
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
        if (_session.TryPause(out var savedButtonCount, out var savedKeyCount))
        {
            Log.Information("[MacroPlayer] Paused at loop {Loop}/{Total} (saved {ButtonCount} buttons, {KeyCount} keys)",
                CurrentLoop, TotalLoops, savedButtonCount, savedKeyCount);
        }
    }

    public void ResumePlayback()
    {
        if (_session.TryResume())
        {
            Log.Information("[MacroPlayer] Resumed");
        }
    }

    public void StopPlayback()
    {
        Log.Information("[MacroPlayer] Stop requested");
        _session.StopPlayback();
    }

    private void CleanupAfterRun()
    {
        IsPlaying = false;
        CurrentLoop = 0;
        TotalLoops = 0;
        IsWaitingBetweenLoops = false;

        _session.End();
        _eventExecutor?.Dispose();
        _eventExecutor = null;
        _coordinator = null;
        Log.Information("[MacroPlayer] ========== PLAYBACK ENDED ==========");
        _ = _runCompleted?.TrySetResult();
    }

    public void Dispose()
    {
        if (!BeginDispose())
        {
            return;
        }

        if (_runCompleted is { } run && !run.Task.Wait(DisposeWaitTimeout))
        {
            Log.Warning("[MacroPlayer] Playback did not stop within {Timeout} while disposing", DisposeWaitTimeout);
        }

        ReleaseResources();
    }

    public async ValueTask DisposeAsync()
    {
        if (!BeginDispose())
        {
            return;
        }

        if (_runCompleted is { } run)
        {
            try
            {
                await run.Task.WaitAsync(DisposeWaitTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Log.Warning("[MacroPlayer] Playback did not stop within {Timeout} while disposing", DisposeWaitTimeout);
            }
        }

        ReleaseResources();
    }

    private bool BeginDispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
        {
            return false;
        }

        StopPlayback();
        return true;
    }

    private void ReleaseResources()
    {
        _eventExecutor?.Dispose();
        _session.Dispose();
    }
}
