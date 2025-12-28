using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using Serilog;
using CrossMacro.Core.Services;

namespace CrossMacro.Core.Services;

public class MacroRecorder : IMacroRecorder, IDisposable
{
    private bool _isRecording;
    private MacroSequence? _currentSequence;
    private Stopwatch? _stopwatch;
    private IInputCapture? _inputCapture;
    private readonly Lock _eventLock = new();
    private readonly IMousePositionProvider? _positionProvider;
    private readonly Func<IInputSimulator>? _inputSimulatorFactory;
    private readonly Func<IInputCapture>? _inputCaptureFactory;
    
    private int _accumulatedX;
    private int _accumulatedY;
    
    private HashSet<int>? _ignoredKeys;
    
    private int _cachedX;
    private int _cachedY;
    private DateTime _lastPositionUpdate = DateTime.MinValue;

    private Task? _syncTask;
    private CancellationTokenSource? _syncCancellation;
    private const int BaseSyncIntervalMs = 1;
    private const int MaxSyncIntervalMs = 500;
    private const int DriftThresholdPx = 0;
    
    private const int PositionCacheMilliseconds = 1;
    private readonly TimeSpan _positionCacheTime = TimeSpan.FromMilliseconds(PositionCacheMilliseconds);
    private const int InitialPositionRetryCount = 3;
    private const int InitialPositionRetryDelayMs = 100;
    
    public event EventHandler<MacroEvent>? EventRecorded;
    
    public bool IsRecording => _isRecording;

    public MacroRecorder(
        IMousePositionProvider positionProvider, 
        Func<IInputSimulator>? inputSimulatorFactory = null,
        Func<IInputCapture>? inputCaptureFactory = null)
    {
        _positionProvider = positionProvider;
        _inputSimulatorFactory = inputSimulatorFactory;
        _inputCaptureFactory = inputCaptureFactory;
        
        if (_positionProvider.IsSupported)
        {
            Log.Information("[MacroRecorder] Using position provider: {ProviderName}", _positionProvider.ProviderName);
        }
        else
        {
            Log.Warning("[MacroRecorder] Position provider not supported, using relative coordinates");
        }
    }

    public async Task StartRecordingAsync(bool recordMouse, bool recordKeyboard, IEnumerable<int>? ignoredKeys = null, CancellationToken cancellationToken = default)
    {
        if (_isRecording)
            return;
            
        if (!recordMouse && !recordKeyboard)
            throw new ArgumentException("At least one recording type (mouse or keyboard) must be enabled");

        _isRecording = true;
        _currentSequence = new MacroSequence
        {
            Name = "New Macro",
            CreatedAt = DateTime.UtcNow,
            IsAbsoluteCoordinates = true
        };
        
        _ignoredKeys = ignoredKeys != null ? new HashSet<int>(ignoredKeys) : null;
        
        _stopwatch = Stopwatch.StartNew();
        
        _pendingRelX = 0;
        _pendingRelY = 0;
        _hasPendingMove = false;
        
        _cachedX = 0;
        _cachedY = 0;
        _accumulatedX = 0;
        _accumulatedY = 0;
        _lastPositionUpdate = DateTime.MinValue;
        
        try
        {
            if (_inputCaptureFactory == null)
            {
                throw new InvalidOperationException("No input capture factory configured. Please provide IInputCapture factory via DI.");
            }
            
            _inputCapture = _inputCaptureFactory();
            _inputCapture.Configure(recordMouse, recordKeyboard);
            _inputCapture.InputReceived += OnInputReceived;
            _inputCapture.Error += OnInputCaptureError;
            
            var devices = _inputCapture.GetAvailableDevices();
            var mice = recordMouse ? devices.Where(d => d.IsMouse).ToList() : [];
            var keyboards = recordKeyboard ? devices.Where(d => d.IsKeyboard).ToList() : [];
            
            if (recordMouse && mice.Count == 0)
            {
                throw new InvalidOperationException("No mouse devices found");
            }
            
            if (recordKeyboard && keyboards.Count == 0)
            {
                throw new InvalidOperationException("No keyboard devices found");
            }
            
            Log.Information("[MacroRecorder] Found {MiceCount} mouse device(s) and {KeyboardCount} keyboard device(s):", mice.Count, keyboards.Count);
            
            foreach (var mouse in mice)
            {
                Log.Information("  [Mouse] {Name} ({Path})", mouse.Name, mouse.Path);
            }
            
            foreach (var keyboard in keyboards)
            {
                Log.Information("  [Keyboard] {Name} ({Path})", keyboard.Name, keyboard.Path);
            }
            
            _ = _inputCapture.StartAsync(cancellationToken);
            
            Log.Information("[MacroRecorder] Input capture started via {ProviderName}", _inputCapture.ProviderName);

            if (recordMouse && _positionProvider != null && _positionProvider.IsSupported)
            {
                try
                {
                    int retryCount = 0;
                    bool positionFound = false;
                    
                    while (retryCount < InitialPositionRetryCount && !positionFound)
                    {
                        var pos = await _positionProvider.GetAbsolutePositionAsync();
                        if (pos.HasValue)
                        {
                            _cachedX = pos.Value.X;
                            _cachedY = pos.Value.Y;
                            Log.Information("[MacroRecorder] Initial position: ({X}, {Y})", _cachedX, _cachedY);
                            positionFound = true;
                        }
                        else
                        {
                            retryCount++;
                            if (retryCount < InitialPositionRetryCount)
                            {
                                Log.Warning("[MacroRecorder] Failed to get initial position, retrying ({Retry}/{Max})...", retryCount, InitialPositionRetryCount);
                                await Task.Delay(InitialPositionRetryDelayMs, cancellationToken);
                            }
                        }
                    }

                    if (!positionFound)
                    {
                        Log.Error("[MacroRecorder] Failed to get initial position after {Max} attempts.", InitialPositionRetryCount);
                        Log.Warning("[MacroRecorder] Falling back to relative recording (0,0 start)");
                        _cachedX = 0;
                        _cachedY = 0;
                        throw new InvalidOperationException("Could not determine initial mouse position. Recording aborted to prevent drift.");
                    }

                    _syncCancellation = new CancellationTokenSource();
                    _syncTask = Task.Run(() => PositionSyncLoop(_syncCancellation.Token));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[MacroRecorder] Error getting initial position");
                    throw;
                }
            }
            else
            {
                Log.Information("[MacroRecorder] Relative recording mode active (Blind Mode)");
                
                try 
                {
                    Log.Information("[MacroRecorder] Performing Corner Reset (Force 0,0) for calibration...");
                    if (_inputSimulatorFactory != null)
                    {
                        using var resetSimulator = _inputSimulatorFactory();
                        resetSimulator.Initialize();
                        await Task.Delay(200, cancellationToken);
                        resetSimulator.MoveRelative(-10000, -10000);
                        await Task.Delay(50, cancellationToken);
                        resetSimulator.MoveRelative(-10000, -10000);
                        Log.Information("[MacroRecorder] Corner Reset complete.");
                    }
                    else
                    {
                        Log.Warning("[MacroRecorder] No input simulator factory, skipping Corner Reset");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[MacroRecorder] Failed to perform Corner Reset");
                }

                _accumulatedX = 0;
                _accumulatedY = 0;
                _cachedX = 0;
                _cachedY = 0;
            }

        }
        catch (Exception)
        {
            _isRecording = false;
            if (_inputCapture != null)
            {
                try 
                { 
                    _inputCapture.InputReceived -= OnInputReceived;
                    _inputCapture.Error -= OnInputCaptureError;
                    _inputCapture.Stop();
                    _inputCapture.Dispose(); 
                } 
                catch (Exception cleanupEx)
                {
                    Log.Debug(cleanupEx, "[MacroRecorder] Error during input capture cleanup");
                }
                _inputCapture = null;
            }
            throw;
        }
    }

    private async Task PositionSyncLoop(CancellationToken cancellationToken)
    {
        if (_positionProvider == null)
            return;

        int currentInterval = BaseSyncIntervalMs;
        int consecutiveFailures = 0;
        
        Log.Information("[MacroRecorder] Position sync loop started (interval: {Interval}ms)", currentInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(currentInterval, cancellationToken);
                
                var sw = Stopwatch.StartNew();
                var actualPos = await _positionProvider.GetAbsolutePositionAsync();
                sw.Stop();
                
                if (actualPos.HasValue)
                {
                    int driftX = Math.Abs(actualPos.Value.X - _cachedX);
                    int driftY = Math.Abs(actualPos.Value.Y - _cachedY);
                    int totalDrift = Math.Max(driftX, driftY);
                    
                    if (totalDrift > DriftThresholdPx)
                    {
                        using (_eventLock.EnterScope())
                        {
                            Log.Debug("[MacroRecorder] Position change detected: ({OldX},{OldY}) -> ({NewX},{NewY}), distance={Drift}px", 
                                _cachedX, _cachedY, actualPos.Value.X, actualPos.Value.Y, totalDrift);
                            
                            if (_isRecording && _currentSequence != null && _stopwatch != null)
                            {
                                var macroEvent = new MacroEvent
                                {
                                    Type = EventType.MouseMove,
                                    Timestamp = _stopwatch.ElapsedMilliseconds,
                                    X = actualPos.Value.X,
                                    Y = actualPos.Value.Y
                                };
                                AddMacroEvent(macroEvent);
                            }
                            
                            _cachedX = actualPos.Value.X;
                            _cachedY = actualPos.Value.Y;
                        }
                    }
                    
                    if (sw.ElapsedMilliseconds > 50)
                    {
                        currentInterval = Math.Min(currentInterval + 50, MaxSyncIntervalMs);
                        Log.Debug("[MacroRecorder] Position query slow ({Ms}ms), increasing interval to {Interval}ms", 
                            sw.ElapsedMilliseconds, currentInterval);
                    }
                    else if (currentInterval > BaseSyncIntervalMs && sw.ElapsedMilliseconds < 10)
                    {
                        currentInterval = Math.Max(currentInterval - 50, BaseSyncIntervalMs);
                    }
                    
                    consecutiveFailures = 0;
                }
                else
                {
                    consecutiveFailures++;
                    if (consecutiveFailures > 3)
                    {
                        currentInterval = Math.Min(currentInterval * 2, MaxSyncIntervalMs);
                        Log.Warning("[MacroRecorder] Position query failed {Count} times, backing off to {Interval}ms", 
                            consecutiveFailures, currentInterval);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacroRecorder] Error in position sync loop");
                consecutiveFailures++;
            }
        }
        
        Log.Information("[MacroRecorder] Position sync loop stopped");
    }

    private void OnInputCaptureError(object? sender, string errorMessage)
    {
        Log.Error("[MacroRecorder] Input capture error: {Error}", errorMessage);
    }

    private int _pendingRelX;
    private int _pendingRelY;
    private bool _hasPendingMove;

    private void OnInputReceived(object? sender, InputCaptureEventArgs e)
    {
        using (_eventLock.EnterScope())
        {
            if (!_isRecording || _currentSequence == null || _stopwatch == null) return;

            switch (e.Type)
            {
                case InputEventType.Sync:
                    FlushPendingMove();
                    break;
                    
                case InputEventType.MouseMove:
                    if (e.Code == InputEventCode.REL_X)
                    {
                        _pendingRelX += e.Value;
                        _hasPendingMove = true;
                    }
                    else if (e.Code == InputEventCode.REL_Y)
                    {
                        _pendingRelY += e.Value;
                        _hasPendingMove = true;
                    }
                    break;
                    
                case InputEventType.MouseScroll:
                    FlushPendingMove();
                    var scrollEvent = new MacroEvent
                    {
                        Timestamp = _stopwatch.ElapsedMilliseconds,
                        Type = EventType.Click,
                        Button = e.Value > 0 ? MouseButton.ScrollUp : MouseButton.ScrollDown
                    };
                    AddMacroEvent(scrollEvent);
                    break;
                
                case InputEventType.MouseButton:
                    // Handle mouse button events (from properly typed platforms)
                    HandleMouseButtonEvent(e);
                    break;
                    
                case InputEventType.Key:
                    if (_hasPendingMove)
                    {
                        FlushPendingMove();
                    }
                    
                    // Backward compatibility: some platforms may still send mouse buttons as Key
                    if (e.Code == InputEventCode.BTN_LEFT || e.Code == InputEventCode.BTN_RIGHT || e.Code == InputEventCode.BTN_MIDDLE)
                    {
                        HandleMouseButtonEvent(e);
                    }
                    else if (e.Code >= 1 && e.Code <= 255)
                    {
                        if (_ignoredKeys != null && _ignoredKeys.Contains(e.Code))
                        {
                            return;
                        }

                        if (e.Value == 0 || e.Value == 1)
                        {
                            var keyEvent = new MacroEvent
                            {
                                Timestamp = _stopwatch.ElapsedMilliseconds,
                                Type = e.Value == 1 ? EventType.KeyPress : EventType.KeyRelease,
                                KeyCode = e.Code,
                                Button = MouseButton.None
                            };
                            
                            Log.Information("[MacroRecorder] Keyboard event: {Type} Key={Code}", 
                                keyEvent.Type, keyEvent.KeyCode);
                            
                            AddMacroEvent(keyEvent);
                        }
                    }
                    break;
            }
        }
    }

    private void HandleMouseButtonEvent(InputCaptureEventArgs e)
    {
        if (_currentSequence == null || _stopwatch == null) return;
        
        if (_hasPendingMove)
        {
            FlushPendingMove();
        }
        
        var buttonEvent = new MacroEvent
        {
            Timestamp = _stopwatch.ElapsedMilliseconds
        };
        
        if (e.Code == InputEventCode.BTN_LEFT) buttonEvent.Button = MouseButton.Left;
        else if (e.Code == InputEventCode.BTN_RIGHT) buttonEvent.Button = MouseButton.Right;
        else if (e.Code == InputEventCode.BTN_MIDDLE) buttonEvent.Button = MouseButton.Middle;
        else return; // Ignore other mouse buttons for recording
        
        buttonEvent.Type = e.Value == 1 ? EventType.ButtonPress : EventType.ButtonRelease;
        
        if (_positionProvider != null)
        {
            buttonEvent.X = _cachedX;
            buttonEvent.Y = _cachedY;
        }
        else
        {
            buttonEvent.X = _accumulatedX;
            buttonEvent.Y = _accumulatedY;
        }
        
        Log.Debug("[MacroRecorder] Mouse button: {Button} {Type} at ({X}, {Y})", 
            buttonEvent.Button, buttonEvent.Type, buttonEvent.X, buttonEvent.Y);
        
        AddMacroEvent(buttonEvent);
    }

    private void FlushPendingMove()
    {
        if (!_hasPendingMove || _currentSequence == null || _stopwatch == null) return;

        var macroEvent = new MacroEvent
        {
            Type = EventType.MouseMove,
            Timestamp = _stopwatch.ElapsedMilliseconds
        };

        if (_positionProvider != null)
        {
            _cachedX += _pendingRelX;
            _cachedY += _pendingRelY;
            macroEvent.X = _cachedX;
            macroEvent.Y = _cachedY;
        }
        else
        {
            _cachedX += _pendingRelX;
            _cachedY += _pendingRelY;
            macroEvent.X = _cachedX;
            macroEvent.Y = _cachedY;
        }

        AddMacroEvent(macroEvent);

        _pendingRelX = 0;
        _pendingRelY = 0;
        _hasPendingMove = false;
    }

    private void AddMacroEvent(MacroEvent macroEvent)
    {
        if (_currentSequence != null)
        {
            if (_currentSequence.Events.Count > 0)
            {
                var lastEvent = _currentSequence.Events[^1];
                macroEvent.DelayMs = (int)(macroEvent.Timestamp - lastEvent.Timestamp);
            }
            else
            {
                macroEvent.DelayMs = 0;
            }

            _currentSequence.Events.Add(macroEvent);
            EventRecorded?.Invoke(this, macroEvent);
        }
    }

    public MacroSequence StopRecording()
    {
        if (!_isRecording)
            throw new InvalidOperationException("Not currently recording");

        Log.Information("[MacroRecorder] Stopping recording...");
        
        if (_syncCancellation != null)
        {
            _syncCancellation.Cancel();
            try
            {
                _syncTask?.Wait(1000);
            }
            catch (AggregateException)
            {
                // Task was cancelled or timed out - expected during shutdown
            }
            
            _syncCancellation?.Dispose();
            _syncCancellation = null;
            _syncTask = null;
        }

        _isRecording = false;
        _stopwatch?.Stop();
        
        if (_inputCapture != null)
        {
            try
            {
                _inputCapture.InputReceived -= OnInputReceived;
                _inputCapture.Error -= OnInputCaptureError;
                _inputCapture.Stop();
                _inputCapture.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacroRecorder] Error stopping input capture");
            }
            _inputCapture = null;
        }

        if (_currentSequence != null && _stopwatch != null)
        {
            _currentSequence.CalculateDuration();
            
            _currentSequence.RecordedAt = DateTime.UtcNow;
            _currentSequence.ActualDuration = _stopwatch.Elapsed;
            
            _currentSequence.MouseMoveCount = _currentSequence.Events.Count(e => e.Type == Models.EventType.MouseMove);
            _currentSequence.ClickCount = _currentSequence.Events.Count(e => 
                e.Type == Models.EventType.Click || 
                e.Type == Models.EventType.ButtonPress || 
                e.Type == Models.EventType.ButtonRelease);
            
            if (_stopwatch.Elapsed.TotalSeconds > 0)
            {
                _currentSequence.EventsPerSecond = _currentSequence.Events.Count / _stopwatch.Elapsed.TotalSeconds;
            }
            
            Log.Information("[MacroRecorder] Recording completed: Duration={Duration:F2}s, Events={Events}, Moves={Moves}, Clicks={Clicks}, EventsPerSec={EventsPerSec:F1}", 
                _stopwatch.Elapsed.TotalSeconds, 
                _currentSequence.Events.Count,
                _currentSequence.MouseMoveCount,
                _currentSequence.ClickCount,
                _currentSequence.EventsPerSecond);
        }

        return _currentSequence ?? new MacroSequence();
    }
    
    public MacroSequence? GetCurrentRecording()
    {
        return _currentSequence;
    }
    
    public void Dispose()
    {
        if (_syncCancellation != null)
        {
            _syncCancellation.Cancel();
            try
            {
                _syncTask?.Wait(1000);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[MacroRecorder] Error waiting for sync task during dispose");
            }
            
            _syncCancellation?.Dispose();
            _syncCancellation = null;
            _syncTask = null;
        }
        
        if (_inputCapture != null)
        {
            try
            {
                _inputCapture.Stop();
                _inputCapture.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacroRecorder] Error disposing input capture");
            }
            _inputCapture = null;
        }
    }
}
