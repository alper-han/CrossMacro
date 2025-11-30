using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Native.Evdev;
using CrossMacro.Native.UInput;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Global hotkey service
/// Monitors keyboard devices for F8/F9/F10 hotkeys
/// </summary>
public class GlobalHotkeyService : IGlobalHotkeyService
{
    private List<EvdevReader> _readers = new();
    private bool _isRunning;
    private readonly Lock _lock = new();
    
    // Key codes (from linux/input-event-codes.h)
    private const int KEY_F8 = 66;
    private const int KEY_F9 = 67;
    private const int KEY_F10 = 68;
    
    // Debouncing - only track hotkeys to prevent memory leak
    private readonly Dictionary<int, DateTime> _lastKeyPressTimes = new()
    {
        { KEY_F8, DateTime.MinValue },
        { KEY_F9, DateTime.MinValue },
        { KEY_F10, DateTime.MinValue }
    };
    private const int DebounceIntervalMs = 300;
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(DebounceIntervalMs);
    
    public event EventHandler? ToggleRecordingRequested;
    public event EventHandler? TogglePlaybackRequested;
    public event EventHandler? TogglePauseRequested;
    
    public bool IsRunning => _isRunning;

    public void Start()
    {
        using (_lock.EnterScope())
        {
            if (_isRunning) return;

            // Auto-detect all keyboard devices
            Log.Information("[GlobalHotkeyService] Auto-detecting keyboard devices...");
            var devices = InputDeviceHelper.GetAvailableDevices();
            var keyboards = devices.Where(d => d.IsKeyboard).ToList();
            
            if (keyboards.Count == 0)
            {
                throw new InvalidOperationException("No keyboard devices found");
            }
            
            Log.Information("[GlobalHotkeyService] Found {Count} keyboard device(s):", keyboards.Count);
            foreach (var kbd in keyboards)
            {
                Log.Information("  - {Name} ({Path})", kbd.Name, kbd.Path);
            }
            
            // Create a reader for each keyboard
            foreach (var kbd in keyboards)
            {
                try
                {
                    var reader = new EvdevReader(kbd.Path, kbd.Name);
                    reader.EventReceived += OnEventReceived;
                    reader.ErrorOccurred += OnError;
                    reader.Start();
                    _readers.Add(reader);
                    Log.Information("[GlobalHotkeyService] Started monitoring: {Name}", kbd.Name);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[GlobalHotkeyService] Failed to open {Name}", kbd.Name);
                    // Continue with other devices
                }
            }
            
            if (_readers.Count == 0)
            {
                throw new InvalidOperationException("Failed to open any keyboard devices");
            }
            
            _isRunning = true;
            Log.Information("[GlobalHotkeyService] Successfully monitoring {Count} keyboard device(s)", _readers.Count);
        }
    }

    public void Stop()
    {
        using (_lock.EnterScope())
        {
            if (!_isRunning) return;

            // Stop all readers in PARALLEL to avoid cumulative delays
            if (_readers.Count > 0)
            {
                // First, unsubscribe from all events
                foreach (var reader in _readers)
                {
                    try
                    {
                        reader.EventReceived -= OnEventReceived;
                        reader.ErrorOccurred -= OnError;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[GlobalHotkeyService] Error unsubscribing from reader events");
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
                        Log.Error(ex, "[GlobalHotkeyService] Error disposing reader");
                    }
                });
                
                _readers.Clear();
            }
            
            _isRunning = false;
            Log.Information("[GlobalHotkeyService] Stopped");
        }
    }

    private void OnEventReceived(EvdevReader sender, UInputNative.input_event ev)
    {
        // We only care about Key Press events (value = 1)
        if (ev.type == UInputNative.EV_KEY && ev.value == 1)
        {
            // Only process hotkeys (F8, F9, F10)
            if (ev.code != KEY_F8 && ev.code != KEY_F9 && ev.code != KEY_F10)
                return;

            using (_lock.EnterScope())
            {
                var now = DateTime.UtcNow;
                if (_lastKeyPressTimes.TryGetValue(ev.code, out var lastTime))
                {
                    if (now - lastTime < _debounceInterval)
                    {
                        Log.Debug("[GlobalHotkeyService] Ignored duplicate key press for code {Code}", ev.code);
                        return;
                    }
                }
                _lastKeyPressTimes[ev.code] = now;
            }

            switch (ev.code)
            {
                case KEY_F8:
                    Log.Information("[GlobalHotkeyService] F8 Pressed - Toggle Recording");
                    ToggleRecordingRequested?.Invoke(this, EventArgs.Empty);
                    break;
                    
                case KEY_F9:
                    Log.Information("[GlobalHotkeyService] F9 Pressed - Toggle Playback");
                    TogglePlaybackRequested?.Invoke(this, EventArgs.Empty);
                    break;
                    
                case KEY_F10:
                    Log.Information("[GlobalHotkeyService] F10 Pressed - Toggle Pause");
                    TogglePauseRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
    }

    private void OnError(Exception ex)
    {
        Log.Error(ex, "[GlobalHotkeyService] Error occurred");
        // Optionally restart or notify
    }

    public void Dispose()
    {
        Stop();
    }
}
