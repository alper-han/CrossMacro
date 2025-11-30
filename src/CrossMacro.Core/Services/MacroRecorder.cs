using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Native.Evdev;
using CrossMacro.Native.UInput;
using Serilog;
using CrossMacro.Core.Wayland;

namespace CrossMacro.Core.Services;

public class MacroRecorder : IMacroRecorder, IDisposable
{
    private bool _isRecording;
    private MacroSequence? _currentSequence;
    private Stopwatch? _stopwatch;
    private List<EvdevReader> _readers = new();
    private readonly Lock _eventLock = new();
    private readonly IMousePositionProvider? _positionProvider;
    
    // Track accumulated relative movement (fallback if no provider)
    private int _accumulatedX;
    private int _accumulatedY;
    
    // Cached mouse position from provider (updated by background sync)
    private int _cachedX;
    private int _cachedY;
    private DateTime _lastPositionUpdate = DateTime.MinValue;

    // Background position sync
    private Task? _syncTask;
    private CancellationTokenSource? _syncCancellation;
    private const int BaseSyncIntervalMs = 1; // Base sync interval (1ms for maximum precision)
    private const int MaxSyncIntervalMs = 500; // Max interval for slow providers
    private const int DriftThresholdPx = 2; // Correction threshold
    
    private const int PositionCacheMilliseconds = 1;
    private readonly TimeSpan _positionCacheTime = TimeSpan.FromMilliseconds(PositionCacheMilliseconds);
    private const int InitialPositionRetryCount = 3;
    private const int InitialPositionRetryDelayMs = 100;
    
    public event EventHandler<MacroEvent>? EventRecorded;
    
    public bool IsRecording => _isRecording;

    public MacroRecorder(IMousePositionProvider positionProvider)
    {
        _positionProvider = positionProvider;
        
        if (_positionProvider.IsSupported)
        {
            Log.Information("[MacroRecorder] Using position provider: {ProviderName}", _positionProvider.ProviderName);
        }
        else
        {
            Log.Warning("[MacroRecorder] Position provider not supported, using relative coordinates");
        }
    }

    // SetInputDevice removed - now auto-detects all mice

    public async Task StartRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (_isRecording)
            return;

        // Reset all state variables for a fresh recording session
        _isRecording = true;
        _currentSequence = new MacroSequence
        {
            Name = "New Macro",
            CreatedAt = DateTime.UtcNow,
            IsAbsoluteCoordinates = true // Always use absolute coordinates to prevent drift
        };
        
        _stopwatch = Stopwatch.StartNew();
        
        // Reset buffering variables
        _pendingRelX = 0;
        _pendingRelY = 0;
        _hasPendingMove = false;
        
        // Reset position tracking
        _cachedX = 0;
        _cachedY = 0;
        _accumulatedX = 0;
        _accumulatedY = 0;
        _lastPositionUpdate = DateTime.MinValue;
        
        try
        {
            // Auto-detect all mouse devices
            Log.Information("[MacroRecorder] Auto-detecting mouse devices...");
            var devices = InputDeviceHelper.GetAvailableDevices();
            var mice = devices.Where(d => d.IsMouse).ToList();
            
            if (mice.Count == 0)
            {
                throw new InvalidOperationException("No mouse devices found");
            }
            
            Log.Information("[MacroRecorder] Found {Count} PHYSICAL mouse device(s) (Virtual devices excluded):", mice.Count);
            foreach (var mouse in mice)
            {
                Log.Information("  - {Name} ({Path})", mouse.Name, mouse.Path);
            }
            
            // Create a reader for each mouse
            foreach (var mouse in mice)
            {
                try
                {
                    var reader = new EvdevReader(mouse.Path, mouse.Name);
                    reader.EventReceived += OnNativeEventReceived;
                    reader.ErrorOccurred += OnReaderError;
                    reader.Start();
                    _readers.Add(reader);
                    Log.Information("[MacroRecorder] Started monitoring: {Name}", mouse.Name);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[MacroRecorder] Failed to open {Name}", mouse.Name);
                    // Continue with other devices
                }
            }
            
            if (_readers.Count == 0)
            {
                throw new InvalidOperationException("Failed to open any mouse devices");
            }
            
            Log.Information("[MacroRecorder] Successfully monitoring {Count} mouse device(s)", _readers.Count);

            // Initialize cached position
            if (_positionProvider != null)
            {
                try
                {
                    // Retry logic for initial position (crucial for correct absolute coordinates)
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
                        throw new InvalidOperationException("Could not determine initial mouse position. Recording aborted to prevent drift.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[MacroRecorder] Error getting initial position");
                }
                
                // Start background position sync to correct drift during recording
                if (_positionProvider != null)
                {
                    _syncCancellation = new CancellationTokenSource();
                    _syncTask = Task.Run(() => PositionSyncLoop(_syncCancellation.Token));
                }
            }
            else
            {
                _accumulatedX = 0;
                _accumulatedY = 0;
            }
        }
        catch (Exception)
        {
            // If anything fails during startup, ensure we clean up any opened readers
            _isRecording = false;
            foreach (var reader in _readers)
            {
                try { reader.Dispose(); } catch { }
            }
            _readers.Clear();
            throw;
        }
    }

    /// <summary>
    /// Background task that periodically syncs cached position with actual cursor position
    /// to correct drift caused by mouse acceleration
    /// </summary>
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
                    // Calculate drift
                    int driftX = Math.Abs(actualPos.Value.X - _cachedX);
                    int driftY = Math.Abs(actualPos.Value.Y - _cachedY);
                    int totalDrift = Math.Max(driftX, driftY);
                    
                    // Apply correction if drift exceeds threshold
                    if (totalDrift > DriftThresholdPx)
                    {
                        using (_eventLock.EnterScope())
                        {
                            Log.Debug("[MacroRecorder] Position drift detected: ({OldX},{OldY}) -> ({NewX},{NewY}), drift={Drift}px", 
                                _cachedX, _cachedY, actualPos.Value.X, actualPos.Value.Y, totalDrift);
                            
                            _cachedX = actualPos.Value.X;
                            _cachedY = actualPos.Value.Y;
                        }
                    }
                    
                    // Adaptive interval based on query time
                    if (sw.ElapsedMilliseconds > 50)
                    {
                        // Slow provider, reduce frequency
                        currentInterval = Math.Min(currentInterval + 50, MaxSyncIntervalMs);
                        Log.Debug("[MacroRecorder] Position query slow ({Ms}ms), increasing interval to {Interval}ms", 
                            sw.ElapsedMilliseconds, currentInterval);
                    }
                    else if (currentInterval > BaseSyncIntervalMs && sw.ElapsedMilliseconds < 10)
                    {
                        // Fast provider, can increase frequency
                        currentInterval = Math.Max(currentInterval - 50, BaseSyncIntervalMs);
                    }
                    
                    consecutiveFailures = 0;
                }
                else
                {
                    consecutiveFailures++;
                    if (consecutiveFailures > 3)
                    {
                        // Multiple failures, back off
                        currentInterval = Math.Min(currentInterval * 2, MaxSyncIntervalMs);
                        Log.Warning("[MacroRecorder] Position query failed {Count} times, backing off to {Interval}ms", 
                            consecutiveFailures, currentInterval);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
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

    private void OnReaderError(Exception ex)
    {
        Log.Error(ex, "[MacroRecorder] Reader error");
        // Optionally stop recording or notify UI
    }

    // Buffering for event grouping
    private int _pendingRelX;
    private int _pendingRelY;
    private bool _hasPendingMove;

    private void OnNativeEventReceived(EvdevReader sender, UInputNative.input_event ev)
    {
        // Lock to handle concurrent events from multiple mice
        using (_eventLock.EnterScope())
        {
            if (!_isRecording || _currentSequence == null || _stopwatch == null) return;

            // Filter events - allow Key, Rel, and Syn
            if (ev.type != UInputNative.EV_KEY && ev.type != UInputNative.EV_REL && ev.type != UInputNative.EV_SYN)
                return;

            // Log significant events with device name
            if (ev.type == UInputNative.EV_KEY)
            {
                Log.Debug("[MacroRecorder] Event from [{Device}]: Type=KEY, Code={Code}, Value={Value}", sender.DeviceName, ev.code, ev.value);
            }

        // Handle SYN events (End of packet)
        if (ev.type == UInputNative.EV_SYN)
        {
            if (ev.code == UInputNative.SYN_REPORT)
            {
                FlushPendingMove();
            }
            return;
        }

        // Handle Relative Movement (Buffer them)
        if (ev.type == UInputNative.EV_REL)
        {
            if (ev.code == UInputNative.REL_X)
            {
                _pendingRelX += ev.value;
                _hasPendingMove = true;
            }
            else if (ev.code == UInputNative.REL_Y)
            {
                _pendingRelY += ev.value;
                _hasPendingMove = true;
            }
            else if (ev.code == 8) // REL_WHEEL
            {
                // Flush any pending move before processing scroll
                FlushPendingMove();

                var macroEvent = new MacroEvent
                {
                    Timestamp = _stopwatch.ElapsedMilliseconds,
                    Type = EventType.Click,
                    Button = ev.value > 0 ? MouseButton.ScrollUp : MouseButton.ScrollDown
                };
                AddMacroEvent(macroEvent);
            }
            return;
        }

        // Handle Keys (Buttons)
        if (ev.type == UInputNative.EV_KEY)
        {
            // If we have pending moves, flush them first so the key happens at the new position
            if (_hasPendingMove)
            {
                FlushPendingMove();
            }

            var macroEvent = new MacroEvent
            {
                Timestamp = _stopwatch.ElapsedMilliseconds
            };

            // Button press/release
            if (ev.code == UInputNative.BTN_LEFT) macroEvent.Button = MouseButton.Left;
            else if (ev.code == UInputNative.BTN_RIGHT) macroEvent.Button = MouseButton.Right;
            else if (ev.code == UInputNative.BTN_MIDDLE) macroEvent.Button = MouseButton.Middle;
            else return; // Ignore other keys

            macroEvent.Type = ev.value == 1 ? EventType.ButtonPress : EventType.ButtonRelease;
            
            // For button events, we want to record the current absolute position if available
            if (_positionProvider != null)
            {
                // We use the cached position, which is updated by FlushPendingMove or on init
                macroEvent.X = _cachedX;
                macroEvent.Y = _cachedY;
            }
            else
            {
                // If no provider, use the accumulated relative coordinates as the "position"
                macroEvent.X = _accumulatedX;
                macroEvent.Y = _accumulatedY;
            }
            
            AddMacroEvent(macroEvent);
        }
        } // end lock
    }

    private void FlushPendingMove()
    {
        if (!_hasPendingMove || _currentSequence == null || _stopwatch == null) return;

        var macroEvent = new MacroEvent
        {
            Type = EventType.MouseMove,
            Timestamp = _stopwatch.ElapsedMilliseconds
        };

        // Use position corrected by background sync thread
        // The sync thread continuously queries actual position and updates _cachedX/_cachedY
        // This is much faster than querying on every event
        if (_positionProvider != null)
        {
            // Background sync has already corrected _cachedX/_cachedY
            // Just apply the pending delta
            _cachedX += _pendingRelX;
            _cachedY += _pendingRelY;
            macroEvent.X = _cachedX;
            macroEvent.Y = _cachedY;
        }
        else
        {
            // No provider: use delta accumulation
            _cachedX += _pendingRelX;
            _cachedY += _pendingRelY;
            macroEvent.X = _cachedX;
            macroEvent.Y = _cachedY;
        }

        AddMacroEvent(macroEvent);

        // Reset buffer
        _pendingRelX = 0;
        _pendingRelY = 0;
        _hasPendingMove = false;
    }

    private void AddMacroEvent(MacroEvent macroEvent)
    {
        if (_currentSequence != null)
        {
            // Calculate delay from previous event
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
        
        // Stop background position sync first
        if (_syncCancellation != null)
        {
            _syncCancellation.Cancel();
            try
            {
                _syncTask?.Wait(1000); // Wait up to 1 second for graceful shutdown
            }
            catch (AggregateException) { /* Ignore cancellation exceptions */ }
            
            _syncCancellation?.Dispose();
            _syncCancellation = null;
            _syncTask = null;
        }

        _isRecording = false;
        _stopwatch?.Stop();
        
        // Stop all readers in parallel for faster shutdown
        if (_readers.Count > 0)
        {
            // First, unsubscribe from all events to prevent new events during shutdown
            foreach (var reader in _readers)
            {
                try
                {
                    reader.EventReceived -= OnNativeEventReceived;
                    reader.ErrorOccurred -= OnReaderError;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[MacroRecorder] Error unsubscribing from reader events");
                }
            }
            
            // Then stop all readers in parallel
            Parallel.ForEach(_readers, reader =>
            {
                try
                {
                    reader.Stop();
                    reader.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[MacroRecorder] Error disposing reader");
                }
            });
            
            _readers.Clear();
        }

        if (_currentSequence != null && _stopwatch != null)
        {
            _currentSequence.CalculateDuration();
            
            // Populate statistics metadata
            _currentSequence.RecordedAt = DateTime.UtcNow;
            _currentSequence.ActualDuration = _stopwatch.Elapsed;
            
            // Count event types
            _currentSequence.MouseMoveCount = _currentSequence.Events.Count(e => e.Type == Models.EventType.MouseMove);
            _currentSequence.ClickCount = _currentSequence.Events.Count(e => 
                e.Type == Models.EventType.Click || 
                e.Type == Models.EventType.ButtonPress || 
                e.Type == Models.EventType.ButtonRelease);
            
            // Calculate events per second
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
        // Note: _positionProvider is NOT disposed here because MacroRecorder
        // is reused across multiple recording sessions. The provider should
        // remain valid for the lifetime of the MacroRecorder instance.
        // Clean up sync task
        if (_syncCancellation != null)
        {
            _syncCancellation.Cancel();
            try
            {
                _syncTask?.Wait(1000);
            }
            catch { /* Ignore */ }
            
            _syncCancellation?.Dispose();
            _syncCancellation = null;
            _syncTask = null;
        }
        
        // Clean up readers
        foreach (var reader in _readers)
        {
            try
            {
                reader.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacroRecorder] Error disposing reader");
            }
        }
        _readers.Clear();
    }
}
