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
    
    private const int VirtualDeviceCreationDelayMs = 500;
    private const int SmallDelayThresholdMs = 15;
    private const double MinimumDelayMs = 0.5;
    private const double MinEnforcedDelayMs = 1.0;
    private const int MaxPlaybackErrors = 10;
    
    public bool IsPlaying { get; private set; }
    public int CurrentLoop { get; private set; }
    public int TotalLoops { get; private set; }
    public bool IsWaitingBetweenLoops { get; private set; }

    public MacroPlayer(IMousePositionProvider positionProvider, PlaybackValidator validator, Func<IInputSimulator>? inputSimulatorFactory = null)
    {
        _positionProvider = positionProvider;
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _inputSimulatorFactory = inputSimulatorFactory;
        
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

            if (_inputSimulatorFactory == null)
                throw new InvalidOperationException("No input simulator factory provided. Ensure IInputSimulator is registered in DI.");
            
            _inputSimulator = _inputSimulatorFactory();
            _inputSimulator.Initialize(_cachedScreenWidth, _cachedScreenHeight);
            Log.Information("[MacroPlayer] Input simulator created: {ProviderName}", _inputSimulator.ProviderName);
            
            await Task.Delay(VirtualDeviceCreationDelayMs, _cts.Token);
            
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
            
            if (firstMouseEvent != null)
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
                Log.Information("[MacroPlayer] No mouse events found in macro, skipping start position move");
            }
            
            int repeatCount = options.Loop ? options.RepeatCount : 1;
            bool infiniteLoop = options.Loop && repeatCount == 0;
            TotalLoops = infiniteLoop ? 0 : repeatCount;
            Log.Information("[MacroPlayer] Loop settings: Loop={Loop}, RepeatCount={Count}, Infinite={Infinite}", options.Loop, repeatCount, infiniteLoop);
            
            int i = 0;
            while ((infiniteLoop || i < repeatCount) && !_cts.Token.IsCancellationRequested)
            {
                CurrentLoop = i + 1;
                Log.Information("[MacroPlayer] Starting playback iteration {Iteration}", i + 1);
                await PlayOnceAsync(macro, options.SpeedMultiplier, _cts.Token);
                
                bool hasNextIteration = infiniteLoop || i < repeatCount - 1;
                
                // Minimum 10ms delay between iterations to ensure hotkey responsiveness
                if (hasNextIteration && !_cts.Token.IsCancellationRequested)
                {
                    int delayMs = Math.Max(10, options.RepeatDelayMs);
                    IsWaitingBetweenLoops = options.RepeatDelayMs > 0;
                    await Task.Delay(delayMs, _cts.Token);
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
            _inputSimulator?.Dispose();
            _inputSimulator = null;
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
                
                if (adjustedDelay > SmallDelayThresholdMs)
                {
                    await Task.Delay((int)adjustedDelay, cancellationToken);
                }
                else if (adjustedDelay > 0)
                {
                    long startTicks = Stopwatch.GetTimestamp();
                    long targetTicks = startTicks + (long)(adjustedDelay * Stopwatch.Frequency / 1000.0);
                    
                    while (Stopwatch.GetTimestamp() < targetTicks)
                    {
                        if (adjustedDelay > 1)
                            Thread.SpinWait(100);
                        else
                            Thread.Yield();
                    }
                }
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
                Log.Information("[MacroPlayer] ButtonPress: {Button} at ({X}, {Y})", ev.Button, ev.X, ev.Y);
                var pressButton = MapButton(ev.Button);
                _inputSimulator.MouseButton(pressButton, true);
                _pressedButtons.TryAdd((ushort)pressButton, 0);
                _isMouseButtonPressed = true;
                break;
                
            case EventType.ButtonRelease:
                Log.Information("[MacroPlayer] ButtonRelease: {Button} at ({X}, {Y})", ev.Button, ev.X, ev.Y);
                var releaseButton = MapButton(ev.Button);
                _inputSimulator.MouseButton(releaseButton, false);
                _pressedButtons.TryRemove((ushort)releaseButton, out _);
                _isMouseButtonPressed = _pressedButtons.Count > 0;
                break;
                
            case EventType.MouseMove:
                bool canPlayAbsolute = (_positionProvider != null && _positionProvider.IsSupported) || (_cachedScreenWidth > 0 && _cachedScreenHeight > 0);

                if (canPlayAbsolute)
                {
                    int targetAbsX, targetAbsY;

                    if (isRecordedAbsolute)
                    {
                        targetAbsX = ev.X;
                        targetAbsY = ev.Y;
                    }
                    else
                    {
                        targetAbsX = _currentX + ev.X;
                        targetAbsY = _currentY + ev.Y;
                    }

                    if (_x11SetPositionMethod != null)
                    {
                        _x11SetPositionMethod.Invoke(_positionProvider, new object[] { targetAbsX, targetAbsY });
                    }
                    else
                    {
                        _inputSimulator.MoveAbsolute(targetAbsX, targetAbsY);
                    }

                    _currentX = targetAbsX;
                    _currentY = targetAbsY;
                }
                else
                {
                    int dx, dy;

                    if (isRecordedAbsolute)
                    {
                        dx = ev.X - _currentX;
                        dy = ev.Y - _currentY;
                        
                        _currentX = ev.X;
                        _currentY = ev.Y;
                    }
                    else
                    {
                        dx = ev.X;
                        dy = ev.Y;
                        
                        _currentX += dx;
                        _currentY += dy;
                    }

                    _inputSimulator.MoveRelative(dx, dy);
                }
                break;
                
            case EventType.Click:
                if (ev.Button == MouseButton.ScrollUp)
                {
                    Log.Information("[MacroPlayer] SCROLL UP");
                    _inputSimulator.Scroll(1);
                }
                else if (ev.Button == MouseButton.ScrollDown)
                {
                    Log.Information("[MacroPlayer] SCROLL DOWN");
                    _inputSimulator.Scroll(-1);
                }
                else
                {
                    Log.Information("[MacroPlayer] CLICK: {Button} at ({X}, {Y})", ev.Button, ev.X, ev.Y);
                    var clickButton = MapButton(ev.Button);
                    _inputSimulator.MouseButton(clickButton, true);
                    _inputSimulator.MouseButton(clickButton, false);
                }
                break;
                
            case EventType.KeyPress:
                Log.Information("[MacroPlayer] KeyPress: KeyCode={KeyCode}", ev.KeyCode);
                _inputSimulator.KeyPress(ev.KeyCode, true);
                _pressedKeys.TryAdd(ev.KeyCode, 0);
                break;
                
            case EventType.KeyRelease:
                Log.Information("[MacroPlayer] KeyRelease: KeyCode={KeyCode}", ev.KeyCode);
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
        ReleaseAllButtons();
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
