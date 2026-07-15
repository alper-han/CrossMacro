using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Core.Services.Playback;
using CrossMacro.Core.Logging;
using CrossMacro.Infrastructure.Services.Playback;
using CrossMacro.Infrastructure.Services.ScreenCapture;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;


public class MacroPlayer : IMacroPlayer, IDisposable, IPlaybackPauseToken, IRunScriptRuntimeVariableSource
{
    private readonly IMousePositionProvider? _positionProvider;
    private readonly IScreenPixelReader? _screenPixelReader;
    private readonly IWindowManager? _windowManager;
    private readonly IClipboardService? _clipboardService;
    private readonly IShellCommandRunner? _shellCommandRunner;
    private readonly IScreenshotCaptureService? _screenshotCaptureService;
    private readonly IImageClickMovementResolver _imageClickMovementResolver;
    private readonly IImageAssetCodec _imageAssetCodec;
    private readonly PlaybackValidator _validator;
    private readonly Func<IInputSimulator>? _inputSimulatorFactory;
    private readonly IInputSimulatorPool? _simulatorPool;
    private readonly IPlaybackTimingService _timingService;
    private readonly Func<TimeSpan, CancellationToken, Task> _playbackWaitAsync;
    private readonly Func<Func<double>> _playbackElapsedMillisecondsFactory;
    private readonly Func<IPlaybackCoordinator> _coordinatorFactory;
    private readonly Func<IButtonStateTracker> _buttonTrackerFactory;
    private readonly Func<IKeyStateTracker> _keyTrackerFactory;
    private readonly IPlaybackMouseButtonMapper _buttonMapper;
    private readonly IPlaybackBehaviorPolicy _playbackBehaviorPolicy;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly MacroPlaybackEventCoordinator _eventCoordinator = new();
    private readonly PlaybackDelayResolver _delayResolver = new();
    private readonly PlaybackSessionResourceOwner _session;

    private IInputSimulator? _inputSimulator;
    private IEventExecutor? _eventExecutor;
    private IPlaybackCoordinator? _coordinator;
    private IButtonStateTracker? _buttonTracker;
    private IKeyStateTracker? _keyTracker;

    private bool _disposed;

    private int _cachedScreenWidth;
    private int _cachedScreenHeight;
    private bool _resolutionCached;

    private int _errorCount;
    private readonly Random _random = Random.Shared;
    private readonly IDictionary<string, string> _runtimeVariables;

    private const double MinEnforcedDelayMs = 1.0;
    private const int MaxPlaybackErrors = 10;
    private const int StabilizationEventCount = 25;
    private const double MaxInitialSpeedMultiplier = 3.0;
    private const int YieldInterval = 50;
    private const int IterationYieldInterval = 50;
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

    private sealed class RuntimeFallbackKeyCodeMapper : IKeyCodeMapper
    {
        public int GetKeyCode(string keyName) => -1;

        public string GetKeyName(int keyCode) => keyCode.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public int GetKeyCodeForCharacter(char character) => -1;

        public char? GetCharacterForKeyCode(int keyCode, bool withShift = false) => null;

        public bool RequiresShift(char character) => false;

        public bool RequiresAltGr(char character) => false;

        public bool IsModifierKeyCode(int keyCode) => false;
    }

    // Pause support

    public bool IsPlaying { get; private set; }
    public int CurrentLoop { get; private set; }
    public int TotalLoops { get; private set; }
    public bool IsWaitingBetweenLoops { get; private set; }
    public bool IsPaused => _session.IsPaused;
    public IReadOnlyDictionary<string, string> RuntimeVariables => _session.RuntimeVariables;

    /// <summary>
    /// Creates a new MacroPlayer with full DI support.
    /// </summary>
    public MacroPlayer(
        IMousePositionProvider? positionProvider,
        PlaybackValidator validator,
        IPlaybackTimingService? timingService = null,
        Func<TimeSpan, CancellationToken, Task>? playbackWaitAsync = null,
        Func<Func<double>>? playbackElapsedMillisecondsFactory = null,
        Func<IPlaybackCoordinator>? coordinatorFactory = null,
        Func<IButtonStateTracker>? buttonTrackerFactory = null,
        Func<IKeyStateTracker>? keyTrackerFactory = null,
        IPlaybackMouseButtonMapper? buttonMapper = null,
        Func<IInputSimulator>? inputSimulatorFactory = null,
        IInputSimulatorPool? simulatorPool = null,
        IPlaybackBehaviorPolicy? playbackBehaviorPolicy = null,
        IScreenPixelReader? screenPixelReader = null,
        IKeyCodeMapper? keyCodeMapper = null,
        IWindowManager? windowManager = null,
        IClipboardService? clipboardService = null,
        IShellCommandRunner? shellCommandRunner = null,
        IScreenshotCaptureService? screenshotCaptureService = null,
        IImageClickMovementResolver? imageClickMovementResolver = null,
        IImageAssetCodec? imageAssetCodec = null)
    {
        _positionProvider = positionProvider;
        _screenPixelReader = screenPixelReader;
        _windowManager = windowManager;
        _clipboardService = clipboardService;
        _shellCommandRunner = shellCommandRunner;
        _screenshotCaptureService = screenshotCaptureService;
        _imageClickMovementResolver = imageClickMovementResolver ?? new ImageClickMovementResolver(positionProvider);
        _imageAssetCodec = imageAssetCodec ?? new ImageAssetCodec();
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _inputSimulatorFactory = inputSimulatorFactory;
        _simulatorPool = simulatorPool;
        _playbackBehaviorPolicy = playbackBehaviorPolicy ?? new PlaybackBehaviorPolicy(useHybridAbsoluteDragMovement: false);

        // Use provided services or create defaults
        _timingService = timingService ?? new PlaybackTimingService();
        _playbackWaitAsync = playbackWaitAsync ?? Task.Delay;
        _playbackElapsedMillisecondsFactory = playbackElapsedMillisecondsFactory ?? CreateRuntimeElapsedMillisecondsProvider;
        _coordinatorFactory = coordinatorFactory
            ?? (() => new DefaultPlaybackCoordinator(positionProvider));
        _buttonTrackerFactory = buttonTrackerFactory ?? (() => new ButtonStateTracker());
        _keyTrackerFactory = keyTrackerFactory ?? (() => new KeyStateTracker());
        _buttonMapper = buttonMapper ?? new DefaultPlaybackMouseButtonMapper();
        _keyCodeMapper = keyCodeMapper ?? new RuntimeFallbackKeyCodeMapper();
        _session = new PlaybackSessionResourceOwner(_playbackWaitAsync, _inputSimulatorFactory, _simulatorPool);
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

        if (_simulatorPool is not null)
        {
            Log.Information("[MacroPlayer] Using InputSimulatorPool for zero-delay device acquisition");
        }
    }

    [Obsolete("Use the IInputSimulatorPool constructor parameter.")]
    public MacroPlayer(
        IMousePositionProvider? positionProvider,
        PlaybackValidator validator,
        IPlaybackTimingService? timingService,
        Func<TimeSpan, CancellationToken, Task>? playbackWaitAsync,
        Func<Func<double>>? playbackElapsedMillisecondsFactory,
        Func<IPlaybackCoordinator>? coordinatorFactory,
        Func<IButtonStateTracker>? buttonTrackerFactory,
        Func<IKeyStateTracker>? keyTrackerFactory,
        IPlaybackMouseButtonMapper? buttonMapper,
        Func<IInputSimulator>? inputSimulatorFactory,
        InputSimulatorPool? simulatorPool,
        IPlaybackBehaviorPolicy? playbackBehaviorPolicy,
        IScreenPixelReader? screenPixelReader,
        IKeyCodeMapper? keyCodeMapper,
        IWindowManager? windowManager,
        IClipboardService? clipboardService,
        IShellCommandRunner? shellCommandRunner,
        IScreenshotCaptureService? screenshotCaptureService,
        IImageClickMovementResolver? imageClickMovementResolver,
        IImageAssetCodec? imageAssetCodec)
        : this(positionProvider, validator, timingService, playbackWaitAsync,
            playbackElapsedMillisecondsFactory, coordinatorFactory, buttonTrackerFactory,
            keyTrackerFactory, buttonMapper, inputSimulatorFactory,
            (IInputSimulatorPool?)simulatorPool, playbackBehaviorPolicy, screenPixelReader,
            keyCodeMapper, windowManager, clipboardService, shellCommandRunner,
            screenshotCaptureService, imageClickMovementResolver, imageAssetCodec)
    {
    }

    #region IPlaybackPauseToken Implementation

    bool IPlaybackPauseToken.IsPaused => _session.IsPaused;

    async Task IPlaybackPauseToken.WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        await _session.WaitIfPausedAsync(cancellationToken);
    }

    #endregion

    public async Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (macro is null)
            throw new ArgumentNullException(nameof(macro));

        if (IsPlaying)
            throw new InvalidOperationException("Playback is already in progress");

        var validationResult = _validator.Validate(macro);
        if (!validationResult.IsValid)
        {
            var errorMsg = string.Join(", ", validationResult.Errors);
            Log.Error("[MacroPlayer] Validation failed: {Error}", errorMsg);
            throw new InvalidOperationException($"Playback validation failed: {errorMsg}");
        }

        foreach (var warning in validationResult.Warnings)
        {
            Log.Warning("[MacroPlayer] Warning: {Warning}", warning);
        }

        options ??= new PlaybackOptions();
        double normalizedSpeed = PlaybackOptions.NormalizeSpeedMultiplier(options.SpeedMultiplier);

        int repeatCount = options.Loop ? options.RepeatCount : 1;
        bool infiniteLoop = options.Loop && repeatCount is 0;
        TotalLoops = infiniteLoop ? 0 : repeatCount;
        CurrentLoop = 1;

        _session.Begin(cancellationToken);
        _inputSimulator = null;
        IsPlaying = true;
        _errorCount = 0;
        _runtimeVariables.Clear();

        Log.Information("[MacroPlayer] ========== PLAYBACK STARTED ==========");

        try
        {
            if (macro.Events.Count is 0 && HasOnlyRuntimeScriptSteps(macro))
            {
                await PlayRuntimeScriptOnlyLoopAsync(macro, options, normalizedSpeed, repeatCount, infiniteLoop, _session.Token);
                return;
            }

            if (macro.Events.Count is 0 && !HasRuntimeScriptSteps(macro))
            {
                await ExecuteScreenReadScriptStepsAsync(macro, _session.Token);
                return;
            }

            await CacheResolutionAsync();
            await AcquireSimulatorAsync(macro);
            EnsureAbsolutePlaybackSupported(macro);
            await InitializePlaybackComponentsAsync(macro);

            Log.Information("[MacroPlayer] Loop settings: Loop={Loop}, RepeatCount={Count}, Infinite={Infinite}",
                options.Loop, repeatCount, infiniteLoop);

            // Stabilization delay
            await _playbackWaitAsync(TimeSpan.FromMilliseconds(50), _session.Token);
            _session.Token.ThrowIfCancellationRequested();

            int iteration = 0;
            while ((infiniteLoop || iteration < repeatCount) && !_session.Token.IsCancellationRequested)
            {
                CurrentLoop = iteration + 1;
                Log.Information("[MacroPlayer] Starting playback iteration {Iteration}", iteration + 1);

                if (iteration > 0)
                {
                        await _coordinator!.PrepareIterationAsync(iteration, macro, _inputSimulator!,
                        _cachedScreenWidth, _cachedScreenHeight, _session.Token);
                }

                if (HasRuntimeScriptSteps(macro))
                {
                    await PlayOnceRuntimeScriptAsync(macro, normalizedSpeed, _session.Token);
                }
                else
                {
                    await PlayOnceAsync(macro, normalizedSpeed, _session.Token);
                }

                // Apply trailing delay after the macro completes (before next iteration or end)
                int trailingDelaySource = ResolveDelayMs(
                    macro.TrailingDelayMs,
                    macro.HasTrailingRandomDelay,
                    macro.TrailingDelayMinMs,
                    macro.TrailingDelayMaxMs);

                if (trailingDelaySource > 0 && !_session.Token.IsCancellationRequested)
                {
                    int trailingDelay = (int)(trailingDelaySource / normalizedSpeed);
                    if (trailingDelay > 0)
                    {
                        await _timingService.WaitAsync(trailingDelay, this, _session.Token);
                    }
                }

                bool hasNextIteration = infiniteLoop || iteration < repeatCount - 1;

                if (hasNextIteration && !_session.Token.IsCancellationRequested)
                {
                    int delayMs = ResolveRepeatDelayMs(options);
                    if (delayMs > 0)
                    {
                        IsWaitingBetweenLoops = true;
                        await _timingService.WaitAsync(delayMs, this, _session.Token);
                        IsWaitingBetweenLoops = false;
                    }
                    else if ((iteration + 1) % IterationYieldInterval is 0)
                    {
                        await Task.Yield();
                    }
                }

                iteration++;
            }
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }
        finally
        {
            Cleanup(macro);
        }
    }

    private async Task CacheResolutionAsync()
    {
        if (!_resolutionCached && _positionProvider is not null)
        {
            try
            {
                var res = await _positionProvider.GetScreenResolutionAsync();
                if (res.HasValue)
                {
                    _cachedScreenWidth = res.Value.Width;
                    _cachedScreenHeight = res.Value.Height;
                    _resolutionCached = true;
                    Log.Information("[MacroPlayer] Screen resolution cached: {Width}x{Height}",
                        _cachedScreenWidth, _cachedScreenHeight);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacroPlayer] Failed to get resolution");
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
        await _session.AcquireAsync(deviceWidth, deviceHeight, _session.Token);
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
        _buttonTracker = _buttonTrackerFactory();
        _keyTracker = _keyTrackerFactory();
        _coordinator = _coordinatorFactory();

        // Create event executor with all dependencies
        _eventExecutor = new MacroEventExecutor(
            _inputSimulator!,
            _buttonTracker,
            _keyTracker,
            _buttonMapper,
            _coordinator,
            useHybridAbsoluteDragMovement: _playbackBehaviorPolicy.UseHybridAbsoluteDragMovement);
        _session.AttachInputState(_eventExecutor, _buttonTracker, _keyTracker);

        _eventExecutor.Initialize(_cachedScreenWidth, _cachedScreenHeight);

        // Initialize coordinator for first iteration
        await _coordinator.InitializeAsync(macro, _inputSimulator!,
            _cachedScreenWidth, _cachedScreenHeight, _session.Token);
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

        await _eventCoordinator.ExecuteAsync(
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
            cancellationToken);

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
            _screenPixelReader ?? NullScreenPixelReader.Instance,
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
        var windowExecutor = new RunScriptWindowExecutor(_windowManager ?? new NullWindowManager());
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
        await runtimeCoordinator.ExecuteAsync(executionRequest, cancellationToken);
    }

    private async Task PlayOnceWithScriptStepsAsync(MacroSequence macro, double speedMultiplier, CancellationToken cancellationToken)
    {
        bool useLegacyCurrentPositionInterpretation = MacroPositionSemantics.IsLegacyCurrentPositionMacro(macro);
        var state = new PlaybackRunState
        {
            ObservedPauseResumeVersion = _session.PauseResumeVersion,
        };
        int totalEvents = macro.Events.Count;
        int eventIndex = 0;
        var playbackElapsedMilliseconds = _playbackElapsedMillisecondsFactory();
        var screenReadExecutor = new RunScriptScreenReadExecutor(
            _screenPixelReader!,
                _positionProvider,
                (ev, token) => ExecutePlaybackEventAsync(
                macro,
                ev,
                speedMultiplier,
                playbackElapsedMilliseconds,
                state,
                useLegacyCurrentPositionInterpretation,
                totalEvents,
                    token),
                _imageClickMovementResolver,
                _inputSimulator,
                _imageAssetCodec);

        Log.Debug("[MacroPlayer] Starting playback of {Total} events at {Speed}x speed", totalEvents, speedMultiplier);

        for (var scriptStepIndex = 0; scriptStepIndex < macro.ScriptSteps.Count; scriptStepIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var step = macro.ScriptSteps[scriptStepIndex];
            if (string.IsNullOrWhiteSpace(step))
            {
                continue;
            }

            if (RunScriptScreenReadExecutor.IsScreenReadingStep(step))
            {
                await screenReadExecutor.ExecuteStepAsync(step, scriptStepIndex + 1, _runtimeVariables, cancellationToken, macro.Images);
                continue;
            }

            if (step.TrimStart().StartsWith("delay ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (eventIndex >= macro.Events.Count)
            {
                throw new InvalidOperationException("Run script playback became out of sync with compiled events.");
            }

            await ExecutePlaybackEventAsync(
                macro,
                macro.Events[eventIndex],
                speedMultiplier,
                playbackElapsedMilliseconds,
                state,
                useLegacyCurrentPositionInterpretation,
                totalEvents,
                cancellationToken);
            eventIndex++;
        }

        if (eventIndex != macro.Events.Count)
        {
            throw new InvalidOperationException("Run script playback did not execute all compiled input events.");
        }

        Log.Debug("[MacroPlayer] Completed playback of {Total} events", totalEvents);
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
            await _session.WaitIfPausedAsync(cancellationToken);
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
                await _timingService.WaitAsync(delayToWait, this, cancellationToken);
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
        catch (Exception ex)
        {
            Log.Error(ex, "[MacroPlayer] Error executing event {Current}/{Total}: {Type}", state.EventCount, totalEvents, ev.Type);
            if (++_errorCount > MaxPlaybackErrors)
            {
                Log.Fatal("[MacroPlayer] Too many errors ({Count}), aborting", _errorCount);
                throw new InvalidOperationException($"Playback aborted after {_errorCount} errors", ex);
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
        await executor.ExecuteAsync(macro, _runtimeVariables, cancellationToken);
    }

    private async Task PlayRuntimeScriptOnlyLoopAsync(
        MacroSequence macro,
        PlaybackOptions options,
        double normalizedSpeed,
        int repeatCount,
        bool infiniteLoop,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_screenPixelReader is null && HasScreenReadingScriptSteps(macro))
        {
            throw new InvalidOperationException("Screen-reading script steps require an IScreenPixelReader runtime service.");
        }

        Log.Information("[MacroPlayer] Loop settings: Loop={Loop}, RepeatCount={Count}, Infinite={Infinite}",
            options.Loop, repeatCount, infiniteLoop);

        if (HasRuntimeInputScriptSteps(macro))
        {
            await CacheResolutionAsync();
            await AcquireSimulatorAsync(macro);
            EnsureAbsolutePlaybackSupported(macro);
            await InitializePlaybackComponentsAsync(macro);
        }

        var iteration = 0;
        while ((infiniteLoop || iteration < repeatCount) && !cancellationToken.IsCancellationRequested)
        {
            CurrentLoop = iteration + 1;
            await PlayOnceRuntimeScriptAsync(macro, normalizedSpeed, cancellationToken);

            var trailingDelaySource = ResolveDelayMs(
                macro.TrailingDelayMs,
                macro.HasTrailingRandomDelay,
                macro.TrailingDelayMinMs,
                macro.TrailingDelayMaxMs);

            if (trailingDelaySource > 0 && !cancellationToken.IsCancellationRequested)
            {
                var trailingDelay = (int)(trailingDelaySource / normalizedSpeed);
                if (trailingDelay > 0)
                {
                    await _timingService.WaitAsync(trailingDelay, this, cancellationToken);
                }
            }

            var hasNextIteration = infiniteLoop || iteration < repeatCount - 1;
            if (hasNextIteration && !cancellationToken.IsCancellationRequested)
            {
                var delayMs = ResolveRepeatDelayMs(options);
                if (delayMs > 0)
                {
                    IsWaitingBetweenLoops = true;
                    await _timingService.WaitAsync(delayMs, this, cancellationToken);
                    IsWaitingBetweenLoops = false;
                }
                else if ((iteration + 1) % IterationYieldInterval is 0)
                {
                    await Task.Yield();
                }
            }

            iteration++;
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

    private static Func<double> CreateRuntimeElapsedMillisecondsProvider()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed.TotalMilliseconds;
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

    public void Resume()
    {
        if (IsPlaying && _session.IsPaused)
        {
            _session.Resume();
            Log.Information("[MacroPlayer] Resumed");
        }
    }

    public void Stop()
    {
        Log.Information("[MacroPlayer] Stop requested");
        _session.Stop();
    }

    private void Cleanup(MacroSequence macro)
    {
        IsPlaying = false;
        CurrentLoop = 0;
        TotalLoops = 0;
        IsWaitingBetweenLoops = false;

        _session.End();
        _eventExecutor?.Dispose();
        _eventExecutor = null;
        _coordinator = null;
        _buttonTracker = null;
        _keyTracker = null;
        Log.Information("[MacroPlayer] ========== PLAYBACK ENDED ==========");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        Stop();
        _session.AttachInputState(executor: null, buttons: null, keys: null);
        _eventExecutor?.Dispose();
        _session.Dispose();

        GC.SuppressFinalize(this);
    }
}
