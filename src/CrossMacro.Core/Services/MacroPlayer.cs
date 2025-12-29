using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Core.Services;

public class MacroPlayer : IMacroPlayer, IDisposable
{
    private IInputSimulator? _inputSimulator;
    private readonly Func<IInputSimulator>? _inputSimulatorFactory;
    private readonly InputSimulatorPool? _simulatorPool;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private readonly IMousePositionProvider? _positionProvider;
    private readonly PlaybackValidator _validator;
    
    private readonly MethodInfo? _x11SetPositionMethod;
    
    
    private int _currentX;
    private int _currentY;
    private bool _positionInitialized;
    private int _cachedScreenWidth;
    private int _cachedScreenHeight;
    private bool _resolutionCached;
    
    private readonly ConcurrentDictionary<ushort, byte> _pressedButtons = new();
    
    private bool _isMouseButtonPressed;
    
    private readonly ConcurrentDictionary<int, byte> _pressedKeys = new();
    
    private int _errorCount;
    
    private const int VirtualDeviceCreationDelayMs = 50;
    private const int SmallDelayThresholdMs = 15;
    private const double MinimumDelayMs = 0.5;
    private const double MinEnforcedDelayMs = 1.0;
    private const int MaxPlaybackErrors = 10;
    
    public bool IsPlaying { get; private set; }
    public int CurrentLoop { get; private set; }
    public int TotalLoops { get; private set; }
    public bool IsWaitingBetweenLoops { get; private set; }

    public MacroPlayer(
        IMousePositionProvider positionProvider, 
        PlaybackValidator validator, 
        Func<IInputSimulator>? inputSimulatorFactory = null,
        InputSimulatorPool? simulatorPool = null)
    {
        _positionProvider = positionProvider;
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _inputSimulatorFactory = inputSimulatorFactory;
        _simulatorPool = simulatorPool;
        
        if (_positionProvider != null)
        {
            // Verify X11 position setter availability (hack for specific Linux provider)
            _x11SetPositionMethod = GetSetPositionMethod(_positionProvider);
            
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
        
        if (validationResult.Warnings.Count > 0)
        {
            foreach (var warning in validationResult.Warnings)
            {
                Log.Warning("[MacroPlayer] Warning: {Warning}", warning);
            }
        }
        
        options ??= new PlaybackOptions();

        int repeatCount = options.Loop ? options.RepeatCount : 1;
        bool infiniteLoop = options.Loop && repeatCount == 0;
        TotalLoops = infiniteLoop ? 0 : repeatCount;
        CurrentLoop = 1;
        
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsPlaying = true;
        
        _positionInitialized = false;
        _currentX = 0;
        _currentY = 0;
        
        _pressedButtons.Clear();
        _pressedKeys.Clear();
        _isMouseButtonPressed = false;
        
        _isPaused = false;
        _pauseEvent.Set(); 
        
        _errorCount = 0;
        
        Log.Information("[MacroPlayer] ========== PLAYBACK STARTED ==========");
        Log.Information("[MacroPlayer] State reset: _positionInitialized={Init}, _currentX={X}, _currentY={Y}", _positionInitialized, _currentX, _currentY);
        
        try
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
                        Log.Information("[MacroPlayer] Screen resolution cached: {Width}x{Height}", _cachedScreenWidth, _cachedScreenHeight);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[MacroPlayer] Failed to get resolution");
                }
            }
            
            Log.Information("[MacroPlayer] Using screen resolution for virtual device: {Width}x{Height}. If this does not match your actual screen resolution, drift will occur.", _cachedScreenWidth, _cachedScreenHeight);

            // Acquire input simulator - prefer pool for zero-delay, fallback to factory
            int deviceWidth = macro.IsAbsoluteCoordinates ? _cachedScreenWidth : 0;
            int deviceHeight = macro.IsAbsoluteCoordinates ? _cachedScreenHeight : 0;
            
            if (_simulatorPool != null)
            {
                // Use pre-warmed device from pool
                _inputSimulator = _simulatorPool.Acquire(deviceWidth, deviceHeight);
                Log.Information("[MacroPlayer] Acquired device from pool: {ProviderName}", _inputSimulator.ProviderName);
                
                // Small stabilization delay to ensure device is ready for first events
                await Task.Delay(20, _cts.Token);
            }
            else if (_inputSimulatorFactory != null)
            {
                // Fallback to factory with delay
                _inputSimulator = _inputSimulatorFactory();
                
                if (macro.IsAbsoluteCoordinates)
                {
                    _inputSimulator.Initialize(_cachedScreenWidth, _cachedScreenHeight);
                    Log.Information("[MacroPlayer] Input simulator created with absolute support: {ProviderName} ({Width}x{Height})", 
                        _inputSimulator.ProviderName, _cachedScreenWidth, _cachedScreenHeight);
                }
                else
                {
                    _inputSimulator.Initialize(0, 0); // Pure relative device
                    Log.Information("[MacroPlayer] Input simulator created as relative-only device: {ProviderName}", 
                        _inputSimulator.ProviderName);
                }
                
                // Only delay when using factory (device not pre-warmed)
                await Task.Delay(VirtualDeviceCreationDelayMs, _cts.Token);
            }
            else
            {
                throw new InvalidOperationException("No input simulator pool or factory provided. Ensure IInputSimulator is registered in DI.");
            }
            
            if (_positionProvider != null && _positionProvider.IsSupported)
            {
                try
                {
                    var pos = await _positionProvider.GetAbsolutePositionAsync();
                    if (pos.HasValue)
                    {
                        _currentX = pos.Value.X;
                        _currentY = pos.Value.Y;
                        _positionInitialized = true;
                        Log.Information("[MacroPlayer] Position initialized from provider: ({X}, {Y})", _currentX, _currentY);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[MacroPlayer] Failed to get initial position from provider");
                }
            }
            
            var firstMouseEvent = macro.Events.FirstOrDefault(e => 
                e.Type == EventType.MouseMove || 
                e.Type == EventType.ButtonPress || 
                e.Type == EventType.ButtonRelease || 
                e.Type == EventType.Click);
            
            if (firstMouseEvent.Type != EventType.None)
            {
                // Only move to start position for absolute coordinate macros
                if (macro.IsAbsoluteCoordinates)
                {
                    if (_cachedScreenWidth > 0 && _cachedScreenHeight > 0)
                    {
                        int startX = Math.Clamp(firstMouseEvent.X, 0, _cachedScreenWidth);
                        int startY = Math.Clamp(firstMouseEvent.Y, 0, _cachedScreenHeight);
                        
                        Log.Information("[MacroPlayer] Moving to start position: ({X}, {Y})", startX, startY);
                        _inputSimulator!.MoveAbsolute(startX, startY);
                        _currentX = startX;
                        _currentY = startY;
                        _positionInitialized = true;
                    }
                    else
                    {
                        Log.Information("[MacroPlayer] Blind Mode: Performing Corner Reset (Force 0,0) to sync start position...");
                        
                        for (int r = 0; r < 5; r++)
                        {
                            _inputSimulator!.MoveRelative(-10000, -10000); 
                            await Task.Delay(20, _cts.Token);
                        }
                        
                        await Task.Delay(100, _cts.Token); 
                        
                        _currentX = 0;
                        _currentY = 0;
                        _positionInitialized = true;
                    }
                }
                else
                {
                    // Relative mode: check if we need Corner Reset
                    if (!macro.SkipInitialZeroZero)
                    {
                        // Recording did Corner Reset, so we should too
                        Log.Information("[MacroPlayer] Relative mode: Performing Corner Reset (0,0) to match recording start...");
                        _inputSimulator!.MoveRelative(-20000, -20000); // Single large move to corner
                        await Task.Delay(10, _cts.Token); // Reduced from 50ms
                        _currentX = 0;
                        _currentY = 0;
                        _positionInitialized = true;
                    }
                    else
                    {
                        // Recording started from wherever cursor was, so do we
                        Log.Information("[MacroPlayer] Relative mode: starting from current position (recording skipped Corner Reset)");
                        _positionInitialized = true;
                    }
                }
            }
            else
            {
                Log.Information("[MacroPlayer] No mouse events found in macro, skipping start position move");
            }
            
            Log.Information("[MacroPlayer] Loop settings: Loop={Loop}, RepeatCount={Count}, Infinite={Infinite}", options.Loop, repeatCount, infiniteLoop);
            
            // Stabilization delay before first playback to ensure device is fully ready
            // This prevents the first few events from being missed
            await Task.Delay(50, _cts.Token);
            
            int i = 0;
            while ((infiniteLoop || i < repeatCount) && !_cts.Token.IsCancellationRequested)
            {
                CurrentLoop = i + 1;
                Log.Information("[MacroPlayer] Starting playback iteration {Iteration}", i + 1);
                
                // For subsequent iterations, reset to start position again
                if (i > 0)
                {
                    if (macro.IsAbsoluteCoordinates && _cachedScreenWidth > 0 && _cachedScreenHeight > 0)
                    {
                        // Absolute mode: move to first event's position
                        var firstEvent = macro.Events.FirstOrDefault(e => e.Type == EventType.MouseMove);
                        if (firstEvent.Type != EventType.None)
                        {
                            int startX = Math.Clamp(firstEvent.X, 0, _cachedScreenWidth);
                            int startY = Math.Clamp(firstEvent.Y, 0, _cachedScreenHeight);
                            _inputSimulator!.MoveAbsolute(startX, startY);
                            _currentX = startX;
                            _currentY = startY;
                        }
                    }
                    else if (!macro.IsAbsoluteCoordinates && !macro.SkipInitialZeroZero)
                    {
                        // Relative mode with Corner Reset: go to 0,0 again
                        Log.Information("[MacroPlayer] Iteration {I}: Performing Corner Reset (0,0)", i + 1);
                        _inputSimulator!.MoveRelative(-20000, -20000);
                        await Task.Delay(10, _cts.Token); // Reduced from 50ms
                        _currentX = 0;
                        _currentY = 0;
                    }
                    // If SkipInitialZeroZero=true, just continue from current position
                }
                
                await PlayOnceAsync(macro, options.SpeedMultiplier, _cts.Token);
                
                bool hasNextIteration = infiniteLoop || i < repeatCount - 1;
                
                // Minimum 10ms delay between iterations to ensure hotkey responsiveness
                if (hasNextIteration && !_cts.Token.IsCancellationRequested)
                {
                    int delayMs = Math.Max(10, options.RepeatDelayMs);
                    IsWaitingBetweenLoops = options.RepeatDelayMs > 0;
                    await WaitWithPauseCheckAsync(delayMs, _cts.Token);
                    IsWaitingBetweenLoops = false;
                }
                
                i++;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            ReleaseAllButtons();
            ReleaseAllKeys();
            
            IsPlaying = false;
            CurrentLoop = 0;
            TotalLoops = 0;
            IsWaitingBetweenLoops = false;
            
            // Return device to pool for warming up replacement, or dispose if no pool
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
            
            _cts?.Dispose();
            _cts = null;
            Log.Information("[MacroPlayer] ========== PLAYBACK ENDED ==========");
        }
    }
    
    private readonly ManualResetEventSlim _pauseEvent = new(true);
    private bool _isPaused;

    public bool IsPaused => _isPaused;

    public void Pause()
    {
        if (IsPlaying && !_isPaused)
        {
            _isPaused = true;
            ReleaseAllButtons();
            ReleaseAllKeys();
            _pauseEvent.Reset();
            Log.Information("[MacroPlayer] Paused");
        }
    }

    public void Resume()
    {
        if (IsPlaying && _isPaused)
        {
            _isPaused = false;
            _pauseEvent.Set();
            Log.Information("[MacroPlayer] Resumed");
        }
    }

    /// <summary>
    /// Waits for the specified delay while checking for pause state.
    /// If paused, immediately stops waiting and blocks until resumed.
    /// </summary>
    private async Task WaitWithPauseCheckAsync(int delayMs, CancellationToken cancellationToken)
    {
        const int checkIntervalMs = 50;
        
        if (delayMs <= 0)
            return;
        
        int remaining = delayMs;
        var sw = Stopwatch.StartNew();
        
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Check for pause state
            if (_isPaused)
            {
                sw.Stop();
                Log.Debug("[MacroPlayer] Pause detected during delay, {Remaining}ms remaining", remaining);
                await Task.Run(() => _pauseEvent.Wait(cancellationToken), cancellationToken);
                sw.Restart();
                Log.Debug("[MacroPlayer] Resumed, continuing with {Remaining}ms delay", remaining);
            }
            
            int waitTime = Math.Min(remaining, checkIntervalMs);
            
            if (waitTime > SmallDelayThresholdMs)
            {
                await Task.Delay(waitTime, cancellationToken);
            }
            else if (waitTime > 0)
            {
                long startTicks = Stopwatch.GetTimestamp();
                long targetTicks = startTicks + (long)(waitTime * Stopwatch.Frequency / 1000.0);
                
                while (Stopwatch.GetTimestamp() < targetTicks)
                {
                    if (waitTime > 1)
                        Thread.SpinWait(100);
                    else
                        Thread.Yield();
                    
                    // Also check for pause during spin-wait for responsiveness
                    if (_isPaused)
                        break;
                }
            }
            
            remaining -= waitTime;
        }
    }

    private async Task PlayOnceAsync(MacroSequence macro, double speedMultiplier, CancellationToken cancellationToken)
    {
        if (_inputSimulator == null)
            throw new InvalidOperationException("Input simulator not initialized");
        
        MacroEvent? previousEvent = null;
        
        foreach (var ev in macro.Events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_isPaused)
            {
                await Task.Run(() => _pauseEvent.Wait(cancellationToken), cancellationToken);
            }
            
            if (previousEvent != null && ev.DelayMs > 0)
            {
                double adjustedDelay = ev.DelayMs / speedMultiplier;
                
                if (_isMouseButtonPressed && adjustedDelay < MinEnforcedDelayMs)
                {
                    adjustedDelay = MinEnforcedDelayMs;
                }
                
                await WaitWithPauseCheckAsync((int)adjustedDelay, cancellationToken);
            }
            
            try
            {
                ExecuteEvent(ev, macro.IsAbsoluteCoordinates);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacroPlayer] Error executing event: {Type}", ev.Type);
                if (++_errorCount > MaxPlaybackErrors)
                {
                    Log.Fatal("[MacroPlayer] Too many errors during playback ({Count}), aborting", _errorCount);
                    throw new InvalidOperationException($"Playback aborted after {_errorCount} errors", ex);
                }
            }
            
            previousEvent = ev;
        }
    }
    
    private void ExecuteEvent(MacroEvent ev, bool isRecordedAbsolute)
    {
        if (_inputSimulator == null)
            return;
        
        if (ev.Type != EventType.MouseMove)
        {
            Log.Debug("[MacroPlayer] Sending event to Virtual Device");
        }
        
        switch (ev.Type)
        {
            case EventType.ButtonPress:
                if (Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
                {
                    Log.Information("[MacroPlayer] ButtonPress: {Button} at ({X}, {Y})", ev.Button, ev.X, ev.Y);
                }
                var pressButton = MapButton(ev.Button);
                _inputSimulator.MouseButton(pressButton, true);
                _pressedButtons.TryAdd((ushort)pressButton, 0);
                _isMouseButtonPressed = true;
                break;
                
            case EventType.ButtonRelease:
                if (Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
                {
                    Log.Information("[MacroPlayer] ButtonRelease: {Button} at ({X}, {Y})", ev.Button, ev.X, ev.Y);
                }
                var releaseButton = MapButton(ev.Button);
                _inputSimulator.MouseButton(releaseButton, false);
                _pressedButtons.TryRemove((ushort)releaseButton, out _);
                _isMouseButtonPressed = _pressedButtons.Count > 0;
                break;
                
            case EventType.MouseMove:
                if (isRecordedAbsolute)
                {
                    bool canPlayAbsolute = (_positionProvider != null && _positionProvider.IsSupported) 
                                        || (_cachedScreenWidth > 0 && _cachedScreenHeight > 0);
                    
                    if (canPlayAbsolute)
                    {
                        if (_x11SetPositionMethod != null)
                        {
                            _x11SetPositionMethod.Invoke(_positionProvider, new object[] { ev.X, ev.Y });
                        }
                        else
                        {
                            _inputSimulator.MoveAbsolute(ev.X, ev.Y);
                        }
                        _currentX = ev.X;
                        _currentY = ev.Y;
                    }
                    else
                    {
                        int dx = ev.X - _currentX;
                        int dy = ev.Y - _currentY;
                        _inputSimulator.MoveRelative(dx, dy);
                        _currentX = ev.X;
                        _currentY = ev.Y;
                    }
                }
                else
                {
                    _inputSimulator.MoveRelative(ev.X, ev.Y);
                    _currentX += ev.X;
                    _currentY += ev.Y;
                }
                break;
                
            case EventType.Click:
                if (ev.Button == MouseButton.ScrollUp)
                {
                    if (Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
                    {
                        Log.Information("[MacroPlayer] SCROLL UP");
                    }
                    _inputSimulator.Scroll(1);
                }
                else if (ev.Button == MouseButton.ScrollDown)
                {
                    if (Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
                    {
                        Log.Information("[MacroPlayer] SCROLL DOWN");
                    }
                    _inputSimulator.Scroll(-1);
                }
                else
                {
                    if (Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
                    {
                        Log.Information("[MacroPlayer] CLICK: {Button} at ({X}, {Y})", ev.Button, ev.X, ev.Y);
                    }
                    var clickButton = MapButton(ev.Button);
                    _inputSimulator.MouseButton(clickButton, true);
                    _inputSimulator.MouseButton(clickButton, false);
                }
                break;
                
            case EventType.KeyPress:
                if (Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
                {
                    Log.Information("[MacroPlayer] KeyPress: KeyCode={KeyCode}", ev.KeyCode);
                }
                _inputSimulator.KeyPress(ev.KeyCode, true);
                _pressedKeys.TryAdd(ev.KeyCode, 0);
                break;
                
            case EventType.KeyRelease:
                if (Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
                {
                    Log.Information("[MacroPlayer] KeyRelease: KeyCode={KeyCode}", ev.KeyCode);
                }
                _inputSimulator.KeyPress(ev.KeyCode, false);
                _pressedKeys.TryRemove(ev.KeyCode, out _);
                break;
        }
    }
    
    private int MapButton(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => MouseButtonCode.Left,
            MouseButton.Right => MouseButtonCode.Right,
            MouseButton.Middle => MouseButtonCode.Middle,
            _ => MouseButtonCode.Left
        };
    }
    
    public void Stop()
    {
        Log.Information("[MacroPlayer] Stop requested");
        ReleaseAllButtons();
        
        // If paused, unblock the wait so cancellation can take effect immediately
        _pauseEvent.Set();
        
        _cts?.Cancel();
    }
    
    private void ReleaseAllButtons()
    {
        if (_inputSimulator == null)
            return;
        
        if (_pressedButtons.IsEmpty)
            return;
            
        Log.Information("[MacroPlayer] Releasing {Count} pressed buttons", _pressedButtons.Count);
        
        var buttonsToRelease = _pressedButtons.Keys.ToArray();
        _pressedButtons.Clear();
        
        foreach (var button in buttonsToRelease)
        {
            try
            {
                _inputSimulator.MouseButton(button, false);
                Log.Debug("[MacroPlayer] Released button: {Button}", button);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacroPlayer] Failed to release button: {Button}", button);
            }
        }
        
        try
        {
            _inputSimulator.MouseButton(MouseButtonCode.Left, false);
            _inputSimulator.MouseButton(MouseButtonCode.Right, false);
            _inputSimulator.MouseButton(MouseButtonCode.Middle, false);
            Log.Debug("[MacroPlayer] Failsafe: Released all common buttons");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MacroPlayer] Failsafe button release failed");
        }
    }
    
    private void ReleaseAllKeys()
    {
        if (_inputSimulator == null)
            return;
        
        if (_pressedKeys.IsEmpty)
            return;
            
        Log.Information("[MacroPlayer] Releasing {Count} pressed keys", _pressedKeys.Count);
        
        var keysToRelease = _pressedKeys.Keys.ToArray();
        _pressedKeys.Clear();
        
        foreach (var keyCode in keysToRelease)
        {
            try
            {
                _inputSimulator.KeyPress(keyCode, false);
                Log.Debug("[MacroPlayer] Released key: {KeyCode}", keyCode);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacroPlayer] Failed to release key: {KeyCode}", keyCode);
            }
        }
    }
    
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        
        Stop();
        ReleaseAllButtons();
        ReleaseAllKeys();
        
        _inputSimulator?.Dispose();
        _cts?.Dispose();
        _pauseEvent?.Dispose();
        
        GC.SuppressFinalize(this);
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Optional Linux-specific method lookup via reflection.")]
    private static MethodInfo? GetSetPositionMethod(IMousePositionProvider provider)
    {
        return provider.GetType().GetMethod("SetAbsolutePositionAsync", new[] { typeof(int), typeof(int) });
    }
}
