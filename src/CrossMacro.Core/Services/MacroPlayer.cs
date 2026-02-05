using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services.Playback;
using Serilog;

namespace CrossMacro.Core.Services;


public class MacroPlayer : IMacroPlayer, IDisposable, IPlaybackPauseToken
{
    private readonly IMousePositionProvider? _positionProvider;
    private readonly PlaybackValidator _validator;
    private readonly Func<IInputSimulator>? _inputSimulatorFactory;
    private readonly InputSimulatorPool? _simulatorPool;
    private readonly IPlaybackTimingService _timingService;
    private readonly Func<IPlaybackCoordinator> _coordinatorFactory;
    private readonly Func<IButtonStateTracker> _buttonTrackerFactory;
    private readonly Func<IKeyStateTracker> _keyTrackerFactory;
    private readonly IPlaybackMouseButtonMapper _buttonMapper;

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

    private int _errorCount;

    private const int VirtualDeviceCreationDelayMs = 50;
    private const double MinEnforcedDelayMs = 1.0;
    private const int MaxPlaybackErrors = 10;
    private const int StabilizationEventCount = 25;
    private const double MaxInitialSpeedMultiplier = 3.0;
    private const int YieldInterval = 50;

    // Pause support
    private readonly ManualResetEventSlim _pauseEvent = new(true);
    private bool _isPaused;
    private ushort[] _pausedButtons = Array.Empty<ushort>();
    private int[] _pausedKeys = Array.Empty<int>();

    public bool IsPlaying { get; private set; }
    public int CurrentLoop { get; private set; }
    public int TotalLoops { get; private set; }
    public bool IsWaitingBetweenLoops { get; private set; }
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Creates a new MacroPlayer with full DI support.
    /// </summary>
    public MacroPlayer(
        IMousePositionProvider? positionProvider,
        PlaybackValidator validator,
        IPlaybackTimingService? timingService = null,
        Func<IPlaybackCoordinator>? coordinatorFactory = null,
        Func<IButtonStateTracker>? buttonTrackerFactory = null,
        Func<IKeyStateTracker>? keyTrackerFactory = null,
        IPlaybackMouseButtonMapper? buttonMapper = null,
        Func<IInputSimulator>? inputSimulatorFactory = null,
        InputSimulatorPool? simulatorPool = null)
    {
        _positionProvider = positionProvider;
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _inputSimulatorFactory = inputSimulatorFactory;
        _simulatorPool = simulatorPool;

        // Use provided services or create defaults
        _timingService = timingService ?? new PlaybackTimingService();
        _coordinatorFactory = coordinatorFactory ?? (() => new DefaultPlaybackCoordinator(positionProvider));
        _buttonTrackerFactory = buttonTrackerFactory ?? (() => new ButtonStateTracker());
        _keyTrackerFactory = keyTrackerFactory ?? (() => new KeyStateTracker());
        _buttonMapper = buttonMapper ?? new DefaultPlaybackMouseButtonMapper();

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

        int repeatCount = options.Loop ? options.RepeatCount : 1;
        bool infiniteLoop = options.Loop && repeatCount == 0;
        TotalLoops = infiniteLoop ? 0 : repeatCount;
        CurrentLoop = 1;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsPlaying = true;
        _isPaused = false;
        _pauseEvent.Set();
        _errorCount = 0;

        Log.Information("[MacroPlayer] ========== PLAYBACK STARTED ==========");

        try
        {
            await CacheResolutionAsync();
            await AcquireSimulatorAsync(macro);
            await InitializePlaybackComponentsAsync(macro);

            Log.Information("[MacroPlayer] Loop settings: Loop={Loop}, RepeatCount={Count}, Infinite={Infinite}",
                options.Loop, repeatCount, infiniteLoop);

            // Stabilization delay
            await Task.Delay(50, _cts.Token);

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

                await PlayOnceAsync(macro, options.SpeedMultiplier, _cts.Token);

                // Apply trailing delay after the macro completes (before next iteration or end)
                if (macro.TrailingDelayMs > 0 && !_cts.Token.IsCancellationRequested)
                {
                    int trailingDelay = (int)(macro.TrailingDelayMs / options.SpeedMultiplier);
                    if (trailingDelay > 0)
                    {
                        await _timingService.WaitAsync(trailingDelay, this, _cts.Token);
                    }
                }

                bool hasNextIteration = infiniteLoop || iteration < repeatCount - 1;

                if (hasNextIteration && !_cts.Token.IsCancellationRequested)
                {
                    int delayMs = Math.Max(10, options.RepeatDelayMs);
                    IsWaitingBetweenLoops = options.RepeatDelayMs > 0;
                    await _timingService.WaitAsync(delayMs, this, _cts.Token);
                    IsWaitingBetweenLoops = false;
                }

                iteration++;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
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

        Log.Information("[MacroPlayer] Using screen resolution: {Width}x{Height}",
            _cachedScreenWidth, _cachedScreenHeight);
    }

    private async Task AcquireSimulatorAsync(MacroSequence macro)
    {
        int deviceWidth = macro.IsAbsoluteCoordinates ? _cachedScreenWidth : 0;
        int deviceHeight = macro.IsAbsoluteCoordinates ? _cachedScreenHeight : 0;

        if (_simulatorPool != null)
        {
            _inputSimulator = _simulatorPool.Acquire(deviceWidth, deviceHeight);
            Log.Information("[MacroPlayer] Acquired device from pool: {ProviderName}", _inputSimulator.ProviderName);
            await Task.Delay(20, _cts!.Token);
        }
        else if (_inputSimulatorFactory != null)
        {
            _inputSimulator = _inputSimulatorFactory();
            _inputSimulator.Initialize(deviceWidth, deviceHeight);
            Log.Information("[MacroPlayer] Input simulator created: {ProviderName}", _inputSimulator.ProviderName);
            await Task.Delay(VirtualDeviceCreationDelayMs, _cts!.Token);
        }
        else
        {
            throw new InvalidOperationException("No input simulator pool or factory provided.");
        }
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
            _coordinator);

        _eventExecutor.Initialize(_cachedScreenWidth, _cachedScreenHeight);

        // Initialize coordinator for first iteration
        await _coordinator.InitializeAsync(macro, _inputSimulator!,
            _cachedScreenWidth, _cachedScreenHeight, _cts!.Token);
    }

    private async Task PlayOnceAsync(MacroSequence macro, double speedMultiplier, CancellationToken cancellationToken)
    {
        int eventCount = 0;
        int totalEvents = macro.Events.Count;
        bool isFirstEvent = true;

        // Accumulate fractional delays to prevent drift from truncation
        // Example: 100 events with 0.9ms each = 90ms total, not 0ms
        double delayAccumulator = 0.0;

        Log.Debug("[MacroPlayer] Starting playback of {Total} events at {Speed}x speed", totalEvents, speedMultiplier);

        foreach (var ev in macro.Events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_isPaused)
            {
                Log.Debug("[MacroPlayer] Paused at event {Current}/{Total}", eventCount, totalEvents);
                await Task.Run(() => _pauseEvent.Wait(cancellationToken), cancellationToken);
                Log.Debug("[MacroPlayer] Resumed playback");
            }

            eventCount++;
            if (eventCount % YieldInterval == 0)
            {
                await Task.Yield();
            }

            if (!isFirstEvent && ev.DelayMs > 0)
            {
                double effectiveSpeed = speedMultiplier;

                // Limit speed for initial events
                if (eventCount <= StabilizationEventCount && speedMultiplier > MaxInitialSpeedMultiplier)
                {
                    effectiveSpeed = MaxInitialSpeedMultiplier;
                }

                double adjustedDelay = ev.DelayMs / effectiveSpeed;

                if (_eventExecutor!.IsMouseButtonPressed && adjustedDelay < MinEnforcedDelayMs)
                {
                    adjustedDelay = MinEnforcedDelayMs;
                }

                // Add accumulated fractional delay from previous truncations
                adjustedDelay += delayAccumulator;

                int delayToWait = (int)adjustedDelay;

                // Carry over the fractional part for next iteration
                delayAccumulator = adjustedDelay - delayToWait;

                if (delayToWait > 0)
                {
                    await _timingService.WaitAsync(delayToWait, this, cancellationToken);
                }
            }
            else if (speedMultiplier > 5.0 && !isFirstEvent)
            {
                await Task.Yield();
            }

            try
            {
                Log.Debug("[MacroPlayer] Executing {Current}/{Total}: {Type} | X={X} Y={Y} | Key={Key} Button={Button}",
                    eventCount, totalEvents, ev.Type, ev.X, ev.Y, ev.KeyCode, ev.Button);

                _eventExecutor!.Execute(ev, macro.IsAbsoluteCoordinates);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacroPlayer] Error executing event {Current}/{Total}: {Type}", eventCount, totalEvents, ev.Type);
                if (++_errorCount > MaxPlaybackErrors)
                {
                    Log.Fatal("[MacroPlayer] Too many errors ({Count}), aborting", _errorCount);
                    throw new InvalidOperationException($"Playback aborted after {_errorCount} errors", ex);
                }
            }

            isFirstEvent = false;
        }

        Log.Debug("[MacroPlayer] Completed playback of {Total} events", totalEvents);
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
                _keyTracker?.RestoreAll(_inputSimulator, _pausedKeys);
            }

            _pausedButtons = Array.Empty<ushort>();
            _pausedKeys = Array.Empty<int>();

            _pauseEvent.Set();
            Log.Information("[MacroPlayer] Resumed");
        }
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
                int deviceWidth = macro.IsAbsoluteCoordinates ? _cachedScreenWidth : 0;
                int deviceHeight = macro.IsAbsoluteCoordinates ? _cachedScreenHeight : 0;
                _simulatorPool.Release(_inputSimulator, deviceWidth, deviceHeight);
            }
            else
            {
                _inputSimulator.Dispose();
            }
            _inputSimulator = null;
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
