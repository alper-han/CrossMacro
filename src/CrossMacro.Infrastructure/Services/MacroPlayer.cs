using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Playback;
using CrossMacro.Core.Logging;
using CrossMacro.Infrastructure.Services.Playback;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;


public class MacroPlayer : IMacroPlayer, IDisposable, IPlaybackPauseToken, IRunScriptRuntimeVariableSource
{
    private readonly IMousePositionProvider? _positionProvider;
    private readonly IScreenPixelReader? _screenPixelReader;
    private readonly IWindowManager? _windowManager;
    private readonly PlaybackValidator _validator;
    private readonly Func<IInputSimulator>? _inputSimulatorFactory;
    private readonly InputSimulatorPool? _simulatorPool;
    private readonly IPlaybackTimingService _timingService;
    private readonly Func<TimeSpan, CancellationToken, Task> _playbackWaitAsync;
    private readonly Func<Func<double>> _playbackElapsedMillisecondsFactory;
    private readonly Func<IPlaybackCoordinator> _coordinatorFactory;
    private readonly Func<IButtonStateTracker> _buttonTrackerFactory;
    private readonly Func<IKeyStateTracker> _keyTrackerFactory;
    private readonly IPlaybackMouseButtonMapper _buttonMapper;
    private readonly IPlaybackBehaviorPolicy _playbackBehaviorPolicy;
    private readonly IKeyCodeMapper _keyCodeMapper;

    private IInputSimulator? _inputSimulator;
    private IEventExecutor? _eventExecutor;
    private IPlaybackCoordinator? _coordinator;
    private IButtonStateTracker? _buttonTracker;
    private IKeyStateTracker? _keyTracker;

    private CancellationTokenSource? _cts;
    private bool _disposed;

    private int _cachedScreenWidth;
    private int _cachedScreenHeight;
    private bool _resolutionCached;
    private int _acquiredSimulatorWidth;
    private int _acquiredSimulatorHeight;

    private int _errorCount;
    private readonly Random _random = Random.Shared;
    private readonly Dictionary<string, string> _runtimeVariables = new(StringComparer.OrdinalIgnoreCase);

    private const int VirtualDeviceCreationDelayMs = 50;
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

    private static readonly int[] RestorableModifierKeys =
    [
        InputEventCode.KEY_LEFTCTRL,
        InputEventCode.KEY_RIGHTCTRL,
        InputEventCode.KEY_LEFTSHIFT,
        InputEventCode.KEY_RIGHTSHIFT,
        InputEventCode.KEY_LEFTALT,
        InputEventCode.KEY_RIGHTALT,
        InputEventCode.KEY_LEFTMETA,
        InputEventCode.KEY_RIGHTMETA
    ];

    // Pause support
    private readonly ManualResetEventSlim _pauseEvent = new(true);
    private volatile bool _isPaused;
    private int _pauseResumeVersion;
    private ushort[] _pausedButtons = Array.Empty<ushort>();
    private int[] _pausedKeys = Array.Empty<int>();

    public bool IsPlaying { get; private set; }
    public int CurrentLoop { get; private set; }
    public int TotalLoops { get; private set; }
    public bool IsWaitingBetweenLoops { get; private set; }
    public bool IsPaused => _isPaused;
    public IReadOnlyDictionary<string, string> RuntimeVariables => _runtimeVariables;

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
        InputSimulatorPool? simulatorPool = null,
        IPlaybackBehaviorPolicy? playbackBehaviorPolicy = null,
        IScreenPixelReader? screenPixelReader = null,
        IKeyCodeMapper? keyCodeMapper = null,
        IWindowManager? windowManager = null)
    {
        _positionProvider = positionProvider;
        _screenPixelReader = screenPixelReader;
        _windowManager = windowManager;
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

        if (_positionProvider != null)
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

        if (_simulatorPool != null)
        {
            Log.Information("[MacroPlayer] Using InputSimulatorPool for zero-delay device acquisition");
        }
    }

    #region IPlaybackPauseToken Implementation

    bool IPlaybackPauseToken.IsPaused => _isPaused;

    async Task IPlaybackPauseToken.WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        if (_isPaused)
        {
            await Task.Run(() => _pauseEvent.Wait(cancellationToken), cancellationToken);
        }
    }

    #endregion

    public async Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (macro == null)
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
        bool infiniteLoop = options.Loop && repeatCount == 0;
        TotalLoops = infiniteLoop ? 0 : repeatCount;
        CurrentLoop = 1;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsPlaying = true;
        _isPaused = false;
        _pauseResumeVersion = 0;
        _pauseEvent.Set();
        _errorCount = 0;
        _runtimeVariables.Clear();

        Log.Information("[MacroPlayer] ========== PLAYBACK STARTED ==========");

        try
        {
            if (macro.Events.Count == 0 && HasOnlyRuntimeScriptSteps(macro))
            {
                await PlayRuntimeScriptOnlyLoopAsync(macro, options, normalizedSpeed, repeatCount, infiniteLoop, _cts.Token);
                return;
            }

            if (macro.Events.Count == 0 && !HasRuntimeScriptSteps(macro))
            {
                await ExecuteScreenReadScriptStepsAsync(macro, _cts.Token);
                return;
            }

            await CacheResolutionAsync();
            await AcquireSimulatorAsync(macro);
            EnsureAbsolutePlaybackSupported(macro);
            await InitializePlaybackComponentsAsync(macro);

            Log.Information("[MacroPlayer] Loop settings: Loop={Loop}, RepeatCount={Count}, Infinite={Infinite}",
                options.Loop, repeatCount, infiniteLoop);

            // Stabilization delay
            await _playbackWaitAsync(TimeSpan.FromMilliseconds(50), _cts.Token);
            _cts.Token.ThrowIfCancellationRequested();

            int iteration = 0;
            while ((infiniteLoop || iteration < repeatCount) && !_cts.Token.IsCancellationRequested)
            {
                CurrentLoop = iteration + 1;
                Log.Information("[MacroPlayer] Starting playback iteration {Iteration}", iteration + 1);

                if (iteration > 0)
                {
                    await _coordinator!.PrepareIterationAsync(iteration, macro, _inputSimulator!,
                        _cachedScreenWidth, _cachedScreenHeight, _cts.Token);
                }

                if (HasRuntimeScriptSteps(macro))
                {
                    await PlayOnceRuntimeScriptAsync(macro, normalizedSpeed, _cts.Token);
                }
                else
                {
                    await PlayOnceAsync(macro, normalizedSpeed, _cts.Token);
                }

                // Apply trailing delay after the macro completes (before next iteration or end)
                int trailingDelaySource = ResolveDelayMs(
                    macro.TrailingDelayMs,
                    macro.HasTrailingRandomDelay,
                    macro.TrailingDelayMinMs,
                    macro.TrailingDelayMaxMs);

                if (trailingDelaySource > 0 && !_cts.Token.IsCancellationRequested)
                {
                    int trailingDelay = (int)(trailingDelaySource / normalizedSpeed);
                    if (trailingDelay > 0)
                    {
                        await _timingService.WaitAsync(trailingDelay, this, _cts.Token);
                    }
                }

                bool hasNextIteration = infiniteLoop || iteration < repeatCount - 1;

                if (hasNextIteration && !_cts.Token.IsCancellationRequested)
                {
                    int delayMs = ResolveRepeatDelayMs(options);
                    if (delayMs > 0)
                    {
                        IsWaitingBetweenLoops = true;
                        await _timingService.WaitAsync(delayMs, this, _cts.Token);
                        IsWaitingBetweenLoops = false;
                    }
                    else if ((iteration + 1) % IterationYieldInterval == 0)
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
        if (!_resolutionCached && _positionProvider != null)
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

        if (_positionProvider != null && !_positionProvider.IsSupported)
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
            || HasAbsoluteRuntimeScriptSteps(macro);
        bool canCreateAbsoluteDevice = needsAbsoluteDevice && _resolutionCached;
        int deviceWidth = canCreateAbsoluteDevice ? _cachedScreenWidth : 0;
        int deviceHeight = canCreateAbsoluteDevice ? _cachedScreenHeight : 0;
        _acquiredSimulatorWidth = deviceWidth;
        _acquiredSimulatorHeight = deviceHeight;

        if (_simulatorPool != null)
        {
            _inputSimulator = _simulatorPool.Acquire(deviceWidth, deviceHeight);
            Log.Information("[MacroPlayer] Acquired device from pool: {ProviderName}", _inputSimulator.ProviderName);
            await _playbackWaitAsync(TimeSpan.FromMilliseconds(20), _cts!.Token);
        }
        else if (_inputSimulatorFactory != null)
        {
            _inputSimulator = _inputSimulatorFactory();
            _inputSimulator.Initialize(deviceWidth, deviceHeight);
            Log.Information("[MacroPlayer] Input simulator created: {ProviderName}", _inputSimulator.ProviderName);
            await _playbackWaitAsync(TimeSpan.FromMilliseconds(VirtualDeviceCreationDelayMs), _cts!.Token);
        }
        else
        {
            throw new InvalidOperationException("No input simulator pool or factory provided.");
        }
    }

    private void EnsureAbsolutePlaybackSupported(MacroSequence macro)
    {
        if (!MacroPositionSemantics.HasAnyAbsoluteCoordinateEvents(macro))
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

        _eventExecutor.Initialize(_cachedScreenWidth, _cachedScreenHeight);

        // Initialize coordinator for first iteration
        await _coordinator.InitializeAsync(macro, _inputSimulator!,
            _cachedScreenWidth, _cachedScreenHeight, _cts!.Token);
    }

    private async Task PlayOnceAsync(MacroSequence macro, double speedMultiplier, CancellationToken cancellationToken)
    {
        bool useLegacyCurrentPositionInterpretation = MacroPositionSemantics.IsLegacyCurrentPositionMacro(macro);
        var state = new PlaybackRunState
        {
            ObservedPauseResumeVersion = Volatile.Read(ref _pauseResumeVersion)
        };
        int totalEvents = macro.Events.Count;
        var playbackElapsedMilliseconds = _playbackElapsedMillisecondsFactory();

        Log.Debug("[MacroPlayer] Starting playback of {Total} events at {Speed}x speed", totalEvents, speedMultiplier);

        foreach (var ev in macro.Events)
        {
            await ExecutePlaybackEventAsync(macro, ev, speedMultiplier, playbackElapsedMilliseconds, state, useLegacyCurrentPositionInterpretation, totalEvents, cancellationToken);
        }

        Log.Debug("[MacroPlayer] Completed playback of {Total} events", totalEvents);
    }

    private async Task PlayOnceRuntimeScriptAsync(MacroSequence macro, double speedMultiplier, CancellationToken cancellationToken)
    {
        bool useLegacyCurrentPositionInterpretation = MacroPositionSemantics.IsLegacyCurrentPositionMacro(macro);
        var state = new PlaybackRunState
        {
            ObservedPauseResumeVersion = Volatile.Read(ref _pauseResumeVersion)
        };
        var playbackElapsedMilliseconds = _playbackElapsedMillisecondsFactory();
        var screenReadExecutor = new RunScriptScreenReadExecutor(_screenPixelReader!, _positionProvider);
        var windowExecutor = new RunScriptWindowExecutor(_windowManager ?? new NullWindowManager());
        var runtimeExecutor = new RunScriptRuntimeExecutor(
            _keyCodeMapper,
            _timingService,
            this,
            _runtimeVariables,
            screenReadExecutor,
            windowExecutor);
        var executionRequest = new RunScriptRuntimeExecutionRequest(
            macro.ScriptSteps,
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

        await runtimeExecutor.ExecuteAsync(executionRequest, cancellationToken);
    }

    private async Task PlayOnceWithScriptStepsAsync(MacroSequence macro, double speedMultiplier, CancellationToken cancellationToken)
    {
        bool useLegacyCurrentPositionInterpretation = MacroPositionSemantics.IsLegacyCurrentPositionMacro(macro);
        var state = new PlaybackRunState
        {
            ObservedPauseResumeVersion = Volatile.Read(ref _pauseResumeVersion)
        };
        int totalEvents = macro.Events.Count;
        int eventIndex = 0;
        var playbackElapsedMilliseconds = _playbackElapsedMillisecondsFactory();
        var screenReadExecutor = new RunScriptScreenReadExecutor(_screenPixelReader!, _positionProvider);

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
                await screenReadExecutor.ExecuteStepAsync(step, scriptStepIndex + 1, _runtimeVariables, cancellationToken);
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

        if (_isPaused)
        {
            Log.Debug("[MacroPlayer] Paused at event {Current}/{Total}", state.EventCount, totalEvents);
            var pausedStartMs = playbackElapsedMilliseconds();
            await Task.Run(() => _pauseEvent.Wait(cancellationToken), cancellationToken);
            var pausedDurationMs = playbackElapsedMilliseconds() - pausedStartMs;
            if (state.HasTimelineAnchor)
            {
                state.TimelineAnchorElapsedMs += pausedDurationMs;
            }
            Log.Debug("[MacroPlayer] Resumed playback");
        }

        int currentPauseResumeVersion = Volatile.Read(ref _pauseResumeVersion);
        if (currentPauseResumeVersion != state.ObservedPauseResumeVersion)
        {
            state.ObservedPauseResumeVersion = currentPauseResumeVersion;
            if (state.HasTimelineAnchor)
            {
                var elapsedSinceAnchorMs = playbackElapsedMilliseconds() - state.TimelineAnchorElapsedMs;
                state.ScheduledElapsedMs = elapsedSinceAnchorMs;
            }
        }

        state.EventCount++;
        if (state.EventCount % YieldInterval == 0)
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
        if (macro.ScriptSteps.Count == 0 || !HasRuntimeScriptSteps(macro))
        {
            return;
        }

        if (_screenPixelReader is null)
        {
            throw new InvalidOperationException("Screen-reading script steps require an IScreenPixelReader runtime service.");
        }

        var executor = new RunScriptScreenReadExecutor(_screenPixelReader, _positionProvider);
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

        if (_screenPixelReader is null)
        {
            throw new InvalidOperationException("Screen-reading script steps require an IScreenPixelReader runtime service.");
        }

        Log.Information("[MacroPlayer] Loop settings: Loop={Loop}, RepeatCount={Count}, Infinite={Infinite}",
            options.Loop, repeatCount, infiniteLoop);

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
                else if ((iteration + 1) % IterationYieldInterval == 0)
                {
                    await Task.Yield();
                }
            }

            iteration++;
        }
    }

    private static bool HasRuntimeScriptSteps(MacroSequence macro)
    {
        return macro.ScriptSteps.Any(step =>
            RunScriptScreenReadExecutor.IsScreenReadingStep(step)
            || RunScriptSyntax.IsWindowStep(step));
    }

    private static bool HasOnlyRuntimeScriptSteps(MacroSequence macro)
    {
        return macro.ScriptSteps.Count > 0
            && macro.ScriptSteps
                .Where(step => !string.IsNullOrWhiteSpace(step))
                .All(step => RunScriptScreenReadExecutor.IsScreenReadingStep(step)
                             || RunScriptSyntax.IsWindowStep(step));
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

    private int ResolveRepeatDelayMs(PlaybackOptions options)
    {
        if (options.UseRandomRepeatDelay)
        {
            return ResolveDelayMs(0, true, options.RepeatDelayMinMs, options.RepeatDelayMaxMs);
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
        int randomDelay = 0;
        if (hasRandomDelay)
        {
            int min = Math.Min(randomDelayMinMs, randomDelayMaxMs);
            int max = Math.Max(randomDelayMinMs, randomDelayMaxMs);
            if (min == max)
            {
                randomDelay = min;
            }
            else if (max == int.MaxValue)
            {
                randomDelay = (int)_random.NextInt64(min, (long)max + 1);
            }
            else
            {
                randomDelay = _random.Next(min, max + 1);
            }
        }

        long totalDelay = (long)fixedDelayMs + randomDelay;
        if (totalDelay <= 0)
            return 0;

        return totalDelay > int.MaxValue ? int.MaxValue : (int)totalDelay;
    }

    private static Func<double> CreateRuntimeElapsedMillisecondsProvider()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed.TotalMilliseconds;
    }

    public void Pause()
    {
        if (IsPlaying && !_isPaused)
        {
            _isPaused = true;

            // Save state before releasing
            _pausedButtons = _buttonTracker?.PressedButtons.ToArray() ?? Array.Empty<ushort>();
            _pausedKeys = _keyTracker?.PressedKeys.ToArray() ?? Array.Empty<int>();

            _eventExecutor?.ReleaseAll();
            _pauseEvent.Reset();

            Log.Information("[MacroPlayer] Paused at loop {Loop}/{Total} (saved {ButtonCount} buttons, {KeyCount} keys)",
                CurrentLoop, TotalLoops, _pausedButtons.Length, _pausedKeys.Length);
        }
    }

    public void Resume()
    {
        if (IsPlaying && _isPaused)
        {
            _isPaused = false;

            // Restore saved state
            if (_inputSimulator != null)
            {
                _buttonTracker?.RestoreAll(_inputSimulator, _pausedButtons);

                if (_keyTracker != null && _pausedKeys.Length > 0)
                {
                    var modifierKeys = _pausedKeys
                        .Where(IsRestorableModifierKey)
                        .ToArray();

                    if (modifierKeys.Length > 0)
                    {
                        _keyTracker.RestoreAll(_inputSimulator, modifierKeys);
                    }

                    int skippedNonModifierCount = _pausedKeys.Length - modifierKeys.Length;
                    if (skippedNonModifierCount > 0)
                    {
                        Log.Debug(
                            "[MacroPlayer] Skipped restoring {Count} non-modifier key(s) on resume to avoid duplicate text input",
                            skippedNonModifierCount);
                    }
                }
            }

            _pausedButtons = Array.Empty<ushort>();
            _pausedKeys = Array.Empty<int>();

            Interlocked.Increment(ref _pauseResumeVersion);
            _pauseEvent.Set();
            Log.Information("[MacroPlayer] Resumed");
        }
    }

    private static bool IsRestorableModifierKey(int keyCode)
    {
        return Array.IndexOf(RestorableModifierKeys, keyCode) >= 0;
    }

    public void Stop()
    {
        Log.Information("[MacroPlayer] Stop requested");
        _eventExecutor?.ReleaseAll();
        _pauseEvent.Set();
        _cts?.Cancel();
    }

    private void Cleanup(MacroSequence macro)
    {
        _eventExecutor?.ReleaseAll();

        IsPlaying = false;
        CurrentLoop = 0;
        TotalLoops = 0;
        IsWaitingBetweenLoops = false;

        // Return or dispose simulator
        if (_inputSimulator != null)
        {
            if (_simulatorPool != null)
            {
                _simulatorPool.Release(_inputSimulator, _acquiredSimulatorWidth, _acquiredSimulatorHeight);
            }
            else
            {
                _inputSimulator.Dispose();
            }
            _inputSimulator = null;
            _acquiredSimulatorWidth = 0;
            _acquiredSimulatorHeight = 0;
        }

        _eventExecutor?.Dispose();
        _eventExecutor = null;
        _coordinator = null;
        _buttonTracker = null;
        _keyTracker = null;

        _cts?.Dispose();
        _cts = null;

        Log.Information("[MacroPlayer] ========== PLAYBACK ENDED ==========");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        Stop();
        _inputSimulator?.Dispose();
        _eventExecutor?.Dispose();
        _cts?.Dispose();
        _pauseEvent?.Dispose();

        GC.SuppressFinalize(this);
    }
}
