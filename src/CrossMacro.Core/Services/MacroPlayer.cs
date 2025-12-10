using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using CrossMacro.Core.Models;
using CrossMacro.Native.UInput;
using CrossMacro.Core.Wayland;
using Serilog;

namespace CrossMacro.Core.Services;

/// <summary>
/// Plays back recorded macro sequences
/// </summary>
public class MacroPlayer : IMacroPlayer, IDisposable
{
    private UInputDevice? _device;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private readonly IMousePositionProvider? _positionProvider;
    private readonly PlaybackValidator _validator;
    
    // Cached reflection for X11 cursor movement (performance optimization)
    private readonly MethodInfo? _x11SetPositionMethod;
    
    // Track current position for absolute coordinate playback
    private int _currentX;
    private int _currentY;
    private bool _positionInitialized;
    private int _cachedScreenWidth;
    private int _cachedScreenHeight;
    private bool _resolutionCached;
    
    // Track pressed mouse buttons to release on stop/pause (thread-safe)
    private readonly ConcurrentDictionary<ushort, byte> _pressedButtons = new();
    
    // Track if any mouse button is currently pressed (for drag operations)
    private bool _isMouseButtonPressed;
    
    // Track pressed keyboard keys to release on stop/pause (thread-safe)
    private readonly ConcurrentDictionary<int, byte> _pressedKeys = new();
    
    // Playback error tracking
    private int _errorCount;
    
    // Constants
    private const int VirtualDeviceCreationDelayMs = 500;
    private const int SmallDelayThresholdMs = 15;
    private const double MinimumDelayMs = 0.5;
    private const double MinEnforcedDelayMs = 1.0; // Minimum delay to prevent event skipping at high speeds
    private const int MaxPlaybackErrors = 10;
    
    public bool IsPlaying { get; private set; }

    public MacroPlayer(IMousePositionProvider positionProvider, PlaybackValidator validator)
    {
        _positionProvider = positionProvider;
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        
        // Cache reflection for X11 SetAbsolutePositionAsync method (one-time cost)
        if (_positionProvider != null)
        {
            _x11SetPositionMethod = _positionProvider.GetType()
                .GetMethod("SetAbsolutePositionAsync", new[] { typeof(int), typeof(int) });
            
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
        
        // Validate macro
        var validationResult = _validator.Validate(macro);
        if (!validationResult.IsValid)
        {
            var errorMsg = string.Join(", ", validationResult.Errors);
            Log.Error("[MacroPlayer] Validation failed: {Error}", errorMsg);
            throw new InvalidOperationException($"Playback validation failed: {errorMsg}");
        }
        
        // Log warnings if any
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
        
        // Reset position tracking state for clean playback
        _positionInitialized = false;
        _currentX = 0;
        _currentY = 0;
        
        // Clear any tracked pressed buttons/keys from previous sessions
        _pressedButtons.Clear();
        _pressedKeys.Clear();
        _isMouseButtonPressed = false;
        
        // Reset pause state for clean playback start
        _isPaused = false;
        _pauseEvent.Set(); // Ensure pause event is signaled (not paused)
        
        // Reset error counter
        _errorCount = 0;
        
        Log.Information("[MacroPlayer] ========== PLAYBACK STARTED ==========");
        Log.Information("[MacroPlayer] State reset: _positionInitialized={Init}, _currentX={X}, _currentY={Y}", _positionInitialized, _currentX, _currentY);
        
        try
        {
            // Get screen resolution if possible (cache to avoid repeated queries)
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

            // Create virtual device
            _device = new UInputDevice(_cachedScreenWidth, _cachedScreenHeight);
            _device.CreateVirtualInputDevice();  // Mouse + Keyboard support
            Log.Information("[MacroPlayer] Virtual device created. All playback events will be sent to this single virtual device.");
            
            // Sleep a bit to ensure device is ready
            await Task.Delay(VirtualDeviceCreationDelayMs, _cts.Token);
            
            // Initialize position from provider if available, otherwise from first event
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
            
            // Explicitly move to start position to ensure clean start
            var firstMouseEvent = macro.Events.FirstOrDefault(e => 
                e.Type == EventType.MouseMove || 
                e.Type == EventType.ButtonPress || 
                e.Type == EventType.ButtonRelease || 
                e.Type == EventType.Click);
            
            if (firstMouseEvent != null)
            {
                if (_cachedScreenWidth > 0 && _cachedScreenHeight > 0)
                {
                    // Full Absolute Support: Move directly to start
                    int startX = Math.Clamp(firstMouseEvent.X, 0, _cachedScreenWidth);
                    int startY = Math.Clamp(firstMouseEvent.Y, 0, _cachedScreenHeight);
                    
                    Log.Information("[MacroPlayer] Moving to start position: ({X}, {Y})", startX, startY);
                    _device.MoveAbsolute(startX, startY);
                    _currentX = startX;
                    _currentY = startY;
                    _positionInitialized = true;
                }
                else
                {
                    Log.Information("[MacroPlayer] Blind Mode: Performing Corner Reset (Force 0,0) to sync start position...");
                    
                    for (int r = 0; r < 5; r++)
                    {
                        _device.Move(-10000, -10000); 
                        await Task.Delay(20, _cts.Token);
                    }
                    
                    await Task.Delay(100, _cts.Token); 
                    
                    _currentX = 0;
                    _currentY = 0;
                    _positionInitialized = true;
                    
                    // If the first event isn't at 0,0, we need to move there from 0,0
                    // Since we are at 0,0 now, we are consistent with the recording start
                }
            }
            else
            {
                Log.Information("[MacroPlayer] No mouse events found in macro, skipping start position move");
            }
            
            // If Loop is enabled, use RepeatCount; otherwise play once
            // RepeatCount = 0 means infinite loop, RepeatCount > 0 means loop that many times
            int repeatCount = options.Loop ? options.RepeatCount : 1;
            bool infiniteLoop = options.Loop && repeatCount == 0;
            Log.Information("[MacroPlayer] Loop settings: Loop={Loop}, RepeatCount={Count}, Infinite={Infinite}", options.Loop, repeatCount, infiniteLoop);
            
            int i = 0;
            while ((infiniteLoop || i < repeatCount) && !_cts.Token.IsCancellationRequested)
            {
                Log.Information("[MacroPlayer] Starting playback iteration {Iteration}", i + 1);
                await PlayOnceAsync(macro, options.SpeedMultiplier, _cts.Token);
                
                if (options.RepeatDelayMs > 0 && !_cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(options.RepeatDelayMs, _cts.Token);
                }
                
                i++;
            }
        }
        catch (OperationCanceledException)
        {
            // Playback was stopped, this is normal
        }
        finally
        {
            // CRITICAL: Release all buttons and keys before cleanup to prevent stuck inputs
            ReleaseAllButtons();
            ReleaseAllKeys();
            
            IsPlaying = false;
            _device?.Dispose();
            _device = null;
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
        if (_device == null)
            throw new InvalidOperationException("Device not initialized");
        
        MacroEvent? previousEvent = null;
        
        foreach (var ev in macro.Events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check for pause
            if (_isPaused)
            {
                await Task.Run(() => _pauseEvent.Wait(cancellationToken), cancellationToken);
            }
            
            // Apply delay
            if (previousEvent != null && ev.DelayMs > 0)
            {
                double adjustedDelay = ev.DelayMs / speedMultiplier;
                
                // Enforce minimum delay ONLY when mouse button is pressed (drag/draw operations)
                // This prevents coordinate skipping during drag operations at high speeds
                // while allowing normal mouse movements to be fully accelerated
                if (_isMouseButtonPressed && adjustedDelay < MinEnforcedDelayMs)
                {
                    adjustedDelay = MinEnforcedDelayMs;
                }
                
                // For delays larger than threshold, Task.Delay is accurate enough and saves CPU
                if (adjustedDelay > SmallDelayThresholdMs)
                {
                    await Task.Delay((int)adjustedDelay, cancellationToken);
                }
                // For small delays, use busy-wait for precision
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
            
            // Execute event
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
        if (_device == null)
            return;
        
        // Log that we are sending to the virtual device (to clarify for user)
        // We log this periodically or for key events to avoid spamming for every mouse move
        if (ev.Type != EventType.MouseMove)
        {
            Log.Debug("[MacroPlayer] Sending event to Virtual Device");
        }
        
        switch (ev.Type)
        {
            case EventType.ButtonPress:
                Log.Information("[MacroPlayer] ButtonPress: {Button} at ({X}, {Y})", ev.Button, ev.X, ev.Y);
                var pressButton = MapButton(ev.Button);
                _device.EmitButton(pressButton, true);
                _pressedButtons.TryAdd(pressButton, 0);
                _isMouseButtonPressed = true; // Track that a button is now pressed
                break;
                
            case EventType.ButtonRelease:
                Log.Information("[MacroPlayer] ButtonRelease: {Button} at ({X}, {Y})", ev.Button, ev.X, ev.Y);
                var releaseButton = MapButton(ev.Button);
                _device.EmitButton(releaseButton, false);
                _pressedButtons.TryRemove(releaseButton, out _);
                _isMouseButtonPressed = _pressedButtons.Count > 0; // Only false if no buttons pressed
                break;
                
            case EventType.MouseMove:
                bool canPlayAbsolute = (_positionProvider != null && _positionProvider.IsSupported) || (_cachedScreenWidth > 0 && _cachedScreenHeight > 0);

                if (canPlayAbsolute)
                {
                    int targetAbsX, targetAbsY;

                    if (isRecordedAbsolute)
                    {
                        // Case 1: Abs -> Abs
                        targetAbsX = ev.X;
                        targetAbsY = ev.Y;
                    }
                    else
                    {
                        // Case 4: Rel -> Abs
                        // ev.X/Y are deltas
                        targetAbsX = _currentX + ev.X;
                        targetAbsY = _currentY + ev.Y;
                    }

                    // On X11, use XWarpPointer instead of uinput absolute positioning
                    // uinput EV_ABS doesn't move the cursor on X11, but XWarpPointer does
                    // Use cached reflection method (set in constructor for performance)
                    if (_x11SetPositionMethod != null)
                    {
                        // X11 provider - use XWarpPointer
                        _x11SetPositionMethod.Invoke(_positionProvider, new object[] { targetAbsX, targetAbsY });
                    }
                    else
                    {
                        // Wayland: use uinput absolute positioning (works fine on Wayland)
                        _device.MoveAbsolute(targetAbsX, targetAbsY);
                    }

                    _currentX = targetAbsX;
                    _currentY = targetAbsY;
                }
                else
                {
                    int dx, dy;

                    if (isRecordedAbsolute)
                    {
                        // Case 3: Abs -> Rel
                        // We need to calculate delta from our "virtual" current position
                        dx = ev.X - _currentX;
                        dy = ev.Y - _currentY;
                        
                        // Update virtual position
                        _currentX = ev.X;
                        _currentY = ev.Y;
                    }
                    else
                    {
                        // Case 2: Rel -> Rel
                        dx = ev.X;
                        dy = ev.Y;
                        
                        // Update virtual position
                        _currentX += dx;
                        _currentY += dy;
                    }

                    _device.Move(dx, dy);
                }
                break;
                
            case EventType.Click:
                // Handle Scroll
                if (ev.Button == MouseButton.ScrollUp)
                {
                    Log.Information("[MacroPlayer] SCROLL UP");
                    _device.SendEvent(UInputNative.EV_REL, UInputNative.REL_WHEEL, 1);
                    _device.SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
                }
                else if (ev.Button == MouseButton.ScrollDown)
                {
                    Log.Information("[MacroPlayer] SCROLL DOWN");
                    _device.SendEvent(UInputNative.EV_REL, UInputNative.REL_WHEEL, -1);
                    _device.SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
                }
                else
                {
                    Log.Information("[MacroPlayer] CLICK: {Button} at ({X}, {Y})", ev.Button, ev.X, ev.Y);
                    // EmitClick does press+release, but we don't need to track it
                    // since it's atomic and completes immediately
                    _device.EmitClick(MapButton(ev.Button));
                }
                break;
                
            case EventType.KeyPress:
                Log.Information("[MacroPlayer] KeyPress: KeyCode={KeyCode}", ev.KeyCode);
                _device.EmitKey(ev.KeyCode, true);
                _pressedKeys.TryAdd(ev.KeyCode, 0);
                break;
                
            case EventType.KeyRelease:
                Log.Information("[MacroPlayer] KeyRelease: KeyCode={KeyCode}", ev.KeyCode);
                _device.EmitKey(ev.KeyCode, false);
                _pressedKeys.TryRemove(ev.KeyCode, out _);
                break;
        }
    }
    
    private ushort MapButton(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => UInputNative.BTN_LEFT,
            MouseButton.Right => UInputNative.BTN_RIGHT,
            MouseButton.Middle => UInputNative.BTN_MIDDLE,
            _ => UInputNative.BTN_LEFT
        };
    }
    
    public void Stop()
    {
        ReleaseAllButtons();
        _cts?.Cancel();
    }
    
    /// <summary>
    /// Release all currently pressed mouse buttons to prevent them from staying stuck
    /// </summary>
    private void ReleaseAllButtons()
    {
        if (_device == null)
            return;
        
        if (_pressedButtons.IsEmpty)
            return;
            
        Log.Information("[MacroPlayer] Releasing {Count} pressed buttons", _pressedButtons.Count);
        
        // Create a copy to avoid modification during iteration
        var buttonsToRelease = _pressedButtons.Keys.ToArray();
        _pressedButtons.Clear();
        
        foreach (var button in buttonsToRelease)
        {
            try
            {
                _device.EmitButton(button, false);
                Log.Debug("[MacroPlayer] Released button: {Button}", button);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacroPlayer] Failed to release button: {Button}", button);
            }
        }
        
        // FAILSAFE: Also explicitly release all common mouse buttons
        // This ensures even if tracking failed, buttons are released
        try
        {
            _device.EmitButton(UInputNative.BTN_LEFT, false);
            _device.EmitButton(UInputNative.BTN_RIGHT, false);
            _device.EmitButton(UInputNative.BTN_MIDDLE, false);
            Log.Debug("[MacroPlayer] Failsafe: Released all common buttons");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MacroPlayer] Failsafe button release failed");
        }
    }
    
    /// <summary>
    /// Release all currently pressed keyboard keys to prevent them from staying stuck
    /// </summary>
    private void ReleaseAllKeys()
    {
        if (_device == null)
            return;
        
        if (_pressedKeys.IsEmpty)
            return;
            
        Log.Information("[MacroPlayer] Releasing {Count} pressed keys", _pressedKeys.Count);
        
        // Create a copy to avoid modification during iteration
        var keysToRelease = _pressedKeys.Keys.ToArray();
        _pressedKeys.Clear();
        
        foreach (var keyCode in keysToRelease)
        {
            try
            {
                _device.EmitKey(keyCode, false);
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
        
        _device?.Dispose();
        _cts?.Dispose();
        _pauseEvent?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
