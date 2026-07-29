
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
    private readonly IPlaybackBehaviorPolicy _playbackBehaviorPolicy;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly PlaybackDelayResolver _delayResolver;
    private readonly PlaybackSessionResourceOwner _session;

    private IInputSimulator? _inputSimulator;
    private MacroEventExecutor? _eventExecutor;
    private IPlaybackCoordinator? _coordinator;

    private bool _disposed;

    private int _cachedScreenWidth;
    private int _cachedScreenHeight;
    private bool _resolutionCached;

    private int _errorCount;
    private readonly IDictionary<string, string> _runtimeVariables;

    private const double MinEnforcedDelayMs = 1.0;
    private const int MaxPlaybackErrors = 10;
    private const int StabilizationEventCount = 25;
    private const double MaxInitialSpeedMultiplier = 3.0;
    private const int YieldInterval = 50;
    private const double MinCatchUpResetDriftMs = 30.0;
    private const double CatchUpResetDelayMultiplier = 2.0;

    private sealed class PlaybackRunState
    {
        public int EventCount;
        public bool IsFirstEvent = true;
        public double ScheduledElapsedMs;
        public double TimelineAnchorElapsedMs;
        public bool HasTimelineAnchor;
        public int ObservedPauseResumeVersion;
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
        _playbackBehaviorPolicy = dependencies.PlaybackBehaviorPolicy;
        _timingService = dependencies.TimingService;
        _playbackWaitAsync = dependencies.PlaybackWaitAsync;
        _playbackElapsedMillisecondsFactory = dependencies.PlaybackElapsedMillisecondsFactory;
        _coordinatorFactory = dependencies.CoordinatorFactory;
        _buttonTrackerFactory = dependencies.ButtonTrackerFactory;
        _keyTrackerFactory = dependencies.KeyTrackerFactory;
        _buttonMapper = dependencies.ButtonMapper;
        _keyCodeMapper = dependencies.KeyCodeMapper;
        _delayResolver = dependencies.DelayResolver;
        _session = new PlaybackSessionResourceOwner(_playbackWaitAsync, dependencies.InputSimulatorFactory, dependencies.SimulatorPool);
        _runtimeVariables = _session.Variables;

        if (_positionProvider is not null)
        {
            if (_positionProvider.IsSupported)
            {
                Log.Information("[MacroPlayer] Using position provider: {ProviderName}", _positionProvider.ProviderName);
            }
            else
            {
                Log.Warning("[MacroPlayer] Position provider not supported, using relative coordinates");
            }
        }

        if (dependencies.SimulatorPool is not null)
        {
            Log.Information("[MacroPlayer] Using InputSimulatorPool for zero-delay device acquisition");
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
            ResolveTrailingDelayMs,
            ResolveRepeatDelayMs,
            (delayMs, token) => _timingService.WaitAsync(delayMs, this, token));

        await lifecycle.RunAsync(macro, options ?? new PlaybackOptions(), cancellationToken).ConfigureAwait(false);
    }

    private void BeginPlayback(CancellationToken cancellationToken)
    {
        _session.Begin(cancellationToken);
        _inputSimulator = null;
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

    private int ResolveTrailingDelayMs(MacroSequence macro)
    {
        return ResolveDelayMs(
            macro.TrailingDelayMs,
            macro.HasTrailingRandomDelay,
            macro.TrailingDelayMinMs,
            macro.TrailingDelayMaxMs);
    }

    private async Task CacheResolutionAsync()
    {
        if (!_resolutionCached && _positionProvider is not null)
        {
            try
            {
                var res = await _positionProvider.GetScreenResolutionAsync().ConfigureAwait(false);
                if (res is not null)
                {
                    _cachedScreenWidth = res.Value.Width;
                    _cachedScreenHeight = res.Value.Height;
                    _resolutionCached = true;
                    Log.Information("[MacroPlayer] Screen resolution cached: {Width}x{Height}",
                        _cachedScreenWidth, _cachedScreenHeight);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[MacroPlayer] Failed to get resolution");
            }
        }

        if (_positionProvider is not null && !_positionProvider.IsSupported)
        {
            Log.Information(
                "[MacroPlayer] Position provider {ProviderName} is resolution-only for playback; absolute playback will require an absolute-capable input simulator",
                _positionProvider.ProviderName);
        }

        Log.Information("[MacroPlayer] Using screen resolution: {Width}x{Height}",
            _cachedScreenWidth, _cachedScreenHeight);
    }

    private async Task AcquireSimulatorAsync(MacroSequence macro)
    {
        bool needsAbsoluteDevice = MacroPositionSemantics.HasAnyAbsoluteCoordinateEvents(macro)
            || HasAbsoluteRuntimeScriptSteps(macro)
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
        if (!MacroPositionSemantics.HasAnyAbsoluteCoordinateEvents(macro) && !HasAbsoluteRuntimeScriptSteps(macro))
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
        // Create per-playback components
        var buttonTracker = _buttonTrackerFactory();
        var keyTracker = _keyTrackerFactory();
        _coordinator = _coordinatorFactory();

        // Create event executor with all dependencies
        _eventExecutor = new MacroEventExecutor(
            _inputSimulator!,
            buttonTracker,
            keyTracker,
            _buttonMapper,
            _coordinator,
            useHybridAbsoluteDragMovement: _playbackBehaviorPolicy.UseHybridAbsoluteDragMovement);
        _session.AttachInputState(_eventExecutor, buttonTracker, keyTracker);

        _eventExecutor.Initialize(_cachedScreenWidth, _cachedScreenHeight);

        // Initialize coordinator for first iteration
        await _coordinator.InitializeAsync(macro, _inputSimulator!,
            _cachedScreenWidth, _cachedScreenHeight, _session.Token).ConfigureAwait(false);
    }

    private async Task PlayOnceAsync(MacroSequence macro, double speedMultiplier, CancellationToken cancellationToken)
    {
        bool useLegacyCurrentPositionInterpretation = MacroPositionSemantics.IsLegacyCurrentPositionMacro(macro);
        var state = new PlaybackRunState
        {
            ObservedPauseResumeVersion = _session.PauseResumeVersion,
        };
        int totalEvents = macro.Events.Count;
        var playbackElapsedMilliseconds = _playbackElapsedMillisecondsFactory();

        Log.Debug("[MacroPlayer] Starting playback of {Total} events at {Speed}x speed", totalEvents, speedMultiplier);

        await MacroPlaybackEventCoordinator.ExecuteAsync(
            macro,
            (ev, token) => ExecutePlaybackEventAsync(
                macro,
                ev,
                speedMultiplier,
                playbackElapsedMilliseconds,
                state,
                useLegacyCurrentPositionInterpretation,
                totalEvents,
                token),
            cancellationToken).ConfigureAwait(false);

        Log.Debug("[MacroPlayer] Completed playback of {Total} events", totalEvents);
    }

    private async Task PlayOnceRuntimeScriptAsync(MacroSequence macro, double speedMultiplier, CancellationToken cancellationToken)
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
                (ev, token) => ExecutePlaybackEventAsync(
                macro,
                ev,
                speedMultiplier,
                playbackElapsedMilliseconds,
                state,
                useLegacyCurrentPositionInterpretation,
                macro.Events.Count,
                    token),
                _imageClickMovementResolver,
                _inputSimulator,
                _imageAssetCodec);
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
        cancellationToken.ThrowIfCancellationRequested();

        if (_session.IsPaused)
        {
            Log.Debug("[MacroPlayer] Paused at event {Current}/{Total}", state.EventCount, totalEvents);
            var pausedStartMs = playbackElapsedMilliseconds();
            await _session.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
            var pausedDurationMs = playbackElapsedMilliseconds() - pausedStartMs;
            if (state.HasTimelineAnchor)
            {
                state.TimelineAnchorElapsedMs += pausedDurationMs;
            }
            Log.Debug("[MacroPlayer] Resumed playback");
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
        if (state.EventCount % YieldInterval is 0)
        {
            await Task.Yield();
        }

        int eventDelaySource = ResolveDelayMs(
            ev.DelayMs,
            ev.HasRandomDelay,
            ev.RandomDelayMinMs,
            ev.RandomDelayMaxMs);

        var waitedForDelay = false;
        if (eventDelaySource > 0)
        {
            double effectiveSpeed = speedMultiplier;

            if (state.EventCount <= StabilizationEventCount && speedMultiplier > MaxInitialSpeedMultiplier)
            {
                effectiveSpeed = MaxInitialSpeedMultiplier;
            }

            double adjustedDelay = eventDelaySource / effectiveSpeed;

            if (_eventExecutor!.IsMouseButtonPressed && adjustedDelay < MinEnforcedDelayMs)
            {
                adjustedDelay = MinEnforcedDelayMs;
            }

            if (!state.HasTimelineAnchor)
            {
                state.TimelineAnchorElapsedMs = playbackElapsedMilliseconds();
                state.HasTimelineAnchor = true;
            }

            state.ScheduledElapsedMs += adjustedDelay;
            var elapsedSinceAnchorMs = playbackElapsedMilliseconds() - state.TimelineAnchorElapsedMs;
            var remainingDelayMs = state.ScheduledElapsedMs - elapsedSinceAnchorMs;
            int delayToWait = (int)Math.Floor(remainingDelayMs);

            if (delayToWait > 0)
            {
                await _timingService.WaitAsync(delayToWait, this, cancellationToken).ConfigureAwait(false);
                waitedForDelay = true;

                elapsedSinceAnchorMs = playbackElapsedMilliseconds() - state.TimelineAnchorElapsedMs;
                remainingDelayMs = state.ScheduledElapsedMs - elapsedSinceAnchorMs;
                if (ShouldResetPlaybackTimeline(remainingDelayMs, adjustedDelay))
                {
                    state.ScheduledElapsedMs = elapsedSinceAnchorMs;
                }
            }
            else if (ShouldResetPlaybackTimeline(remainingDelayMs, adjustedDelay))
            {
                state.ScheduledElapsedMs = elapsedSinceAnchorMs;
            }
        }

        if (!waitedForDelay && speedMultiplier > 5.0 && !state.IsFirstEvent)
        {
            await Task.Yield();
        }

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
            _eventExecutor!.Execute(eventToExecute, coordinateMode);
        }
        catch (AbsolutePlaybackUnsupportedException)
        {
            throw;
        }
        catch (InputInjectionPermissionRequiredException)
        {
            throw;
        }
        catch (ImageClickMovementUnsupportedException)
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

        state.IsFirstEvent = false;
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

    private static bool HasAbsoluteRuntimeScriptSteps(MacroSequence macro)
    {
        return macro.ScriptSteps.Any(step =>
        {
            var trimmed = step.TrimStart();
            return trimmed.StartsWith("move abs ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("move absolute ", StringComparison.OrdinalIgnoreCase);
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
                || trimmed.StartsWith("type ", StringComparison.OrdinalIgnoreCase);
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
