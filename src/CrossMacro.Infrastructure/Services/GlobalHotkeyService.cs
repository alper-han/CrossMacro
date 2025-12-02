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
/// Monitors keyboard devices for customizable hotkeys
/// </summary>
public class GlobalHotkeyService : IGlobalHotkeyService
{
    private List<EvdevReader> _readers = new();
    private bool _isRunning;
    private readonly Lock _lock = new();
    
    // Hotkey mappings (action -> key codes + modifiers)
    private HotkeyMapping _recordingHotkey = new();
    private HotkeyMapping _playbackHotkey = new();
    private HotkeyMapping _pauseHotkey = new();
    
    // Track currently pressed modifiers
    private readonly HashSet<int> _pressedModifiers = new();
    
    // Debouncing
    private readonly Dictionary<string, DateTime> _lastHotkeyPressTimes = new();
    private const int DebounceIntervalMs = 300;
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(DebounceIntervalMs);
    
    // Control whether playback/pause hotkeys are enabled (used during recording)
    private bool _playbackPauseHotkeysEnabled = true;
    
    public event EventHandler? ToggleRecordingRequested;
    public event EventHandler? TogglePlaybackRequested;
    public event EventHandler? TogglePauseRequested;
    
    public bool IsRunning => _isRunning;

    private readonly IHotkeyConfigurationService _configService;

    public GlobalHotkeyService(IHotkeyConfigurationService configService)
    {
        _configService = configService;
        
        // Load saved hotkeys
        var settings = _configService.Load();
        UpdateHotkeys(settings.RecordingHotkey, settings.PlaybackHotkey, settings.PauseHotkey, save: false);
    }

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

    public void UpdateHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey)
    {
        UpdateHotkeys(recordingHotkey, playbackHotkey, pauseHotkey, save: true);
    }

    private void UpdateHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey, bool save)
    {
        using (_lock.EnterScope())
        {
            _recordingHotkey = ParseHotkey(recordingHotkey);
            _playbackHotkey = ParseHotkey(playbackHotkey);
            _pauseHotkey = ParseHotkey(pauseHotkey);
            
            Log.Information("[GlobalHotkeyService] Updated hotkeys: Recording={Recording}, Playback={Playback}, Pause={Pause}",
                recordingHotkey, playbackHotkey, pauseHotkey);
        }

        if (save)
        {
            Task.Run(() => 
            {
                try
                {
                    _configService.Save(new HotkeySettings
                    {
                        RecordingHotkey = recordingHotkey,
                        PlaybackHotkey = playbackHotkey,
                        PauseHotkey = pauseHotkey
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to save hotkeys asynchronously");
                }
            });
        }
    }

    // Capture state
    private TaskCompletionSource<string>? _captureTcs;
    private bool _isCapturing;

    public async Task<string> CaptureNextKeyAsync(CancellationToken cancellationToken = default)
    {
        _captureTcs = new TaskCompletionSource<string>();
        _isCapturing = true;
        
        using (_lock.EnterScope())
        {
            // Clear modifiers for fresh capture
            _pressedModifiers.Clear();
        }

        using (cancellationToken.Register(() => _captureTcs.TrySetCanceled()))
        {
            try
            {
                return await _captureTcs.Task;
            }
            finally
            {
                _isCapturing = false;
                _captureTcs = null;
            }
        }
    }

    private void OnEventReceived(EvdevReader sender, UInputNative.input_event ev)
    {
        if (ev.type != UInputNative.EV_KEY)
            return;

        using (_lock.EnterScope())
        {
            // Track modifier key state
            if (IsModifierKeyCode(ev.code))
            {
                if (ev.value == 1) // Key press
                {
                    _pressedModifiers.Add(ev.code);
                }
                else if (ev.value == 0) // Key release
                {
                    _pressedModifiers.Remove(ev.code);
                }
                return;
            }

            // Only process key press events for main keys
            if (ev.value != 1)
                return;

            // If capturing, handle capture and return
            if (_isCapturing && _captureTcs != null)
            {
                var hotkeyString = BuildHotkeyString(ev.code);
                // Run on thread pool to avoid blocking input thread with TCS continuations
                Task.Run(() => _captureTcs.TrySetResult(hotkeyString));
                return;
            }

            // Check if this matches any hotkey
            // Recording hotkey is always active
            CheckHotkeyMatch(ev.code, "Recording", _recordingHotkey, () => ToggleRecordingRequested?.Invoke(this, EventArgs.Empty));
            
            // Playback and pause hotkeys are only active when enabled (disabled during recording)
            if (_playbackPauseHotkeysEnabled)
            {
                CheckHotkeyMatch(ev.code, "Playback", _playbackHotkey, () => TogglePlaybackRequested?.Invoke(this, EventArgs.Empty));
                CheckHotkeyMatch(ev.code, "Pause", _pauseHotkey, () => TogglePauseRequested?.Invoke(this, EventArgs.Empty));
            }
        }
    }

    private string BuildHotkeyString(int keyCode)
    {
        var parts = new List<string>();

        // Modifiers
        if (_pressedModifiers.Contains(29) || _pressedModifiers.Contains(97)) parts.Add("Ctrl");
        if (_pressedModifiers.Contains(42) || _pressedModifiers.Contains(54)) parts.Add("Shift");
        if (_pressedModifiers.Contains(56) || _pressedModifiers.Contains(100)) parts.Add("Alt");
        if (_pressedModifiers.Contains(125) || _pressedModifiers.Contains(126)) parts.Add("Super");

        // Main Key
        parts.Add(GetKeyName(keyCode));

        return string.Join("+", parts);
    }

    private static string GetKeyName(int keyCode)
    {
        // Reverse mapping
        if (keyCode >= 59 && keyCode <= 82) return "F" + (keyCode - 59 + 1); // F1-F24
        
        // Digits
        if (keyCode == 11) return "0";
        if (keyCode >= 2 && keyCode <= 10) return (keyCode - 1).ToString();

        // Letters
        if (keyCode >= 16 && keyCode <= 25) return "QWERTYUIOP"[keyCode - 16].ToString();
        if (keyCode >= 30 && keyCode <= 38) return "ASDFGHJKL"[keyCode - 30].ToString();
        if (keyCode >= 44 && keyCode <= 50) return "ZXCVBNM"[keyCode - 44].ToString();

        return keyCode switch
        {
            57 => "Space",
            28 => "Enter",
            15 => "Tab",
            14 => "Backspace",
            1 => "Escape",
            111 => "Delete",
            110 => "Insert",
            102 => "Home",
            107 => "End",
            104 => "PageUp",
            109 => "PageDown",
            103 => "Up",
            108 => "Down",
            105 => "Left",
            106 => "Right",
            
            51 => ",",
            52 => ".",
            12 => "-",
            13 => "=",
            39 => ";",
            40 => "'",
            26 => "[",
            27 => "]",
            43 => "\\",
            53 => "/",
            41 => "`",
            
            _ => $"Key{keyCode}"
        };
    }

    private void CheckHotkeyMatch(int keyCode, string actionName, HotkeyMapping mapping, Action action)
    {
        if (mapping.MainKey != keyCode)
            return;

        // Check if all required modifiers are pressed
        if (!mapping.RequiredModifiers.All(m => _pressedModifiers.Contains(m)))
            return;

        // Check if any extra modifiers are pressed
        if (_pressedModifiers.Except(mapping.RequiredModifiers).Any())
            return;

        // Debouncing
        // Note: Caller (OnEventReceived) already holds _lock
        var now = DateTime.UtcNow;
        if (_lastHotkeyPressTimes.TryGetValue(actionName, out var lastTime))
        {
            if (now - lastTime < _debounceInterval)
            {
                return;
            }
        }
        _lastHotkeyPressTimes[actionName] = now;

        Log.Information("[GlobalHotkeyService] {Action} Hotkey Pressed", actionName);
        action();
    }

    private static bool IsModifierKeyCode(int code)
    {
        return code is 29 or 97   // Ctrl (left, right)
            or 42 or 54           // Shift (left, right)
            or 56 or 100          // Alt (left, right)
            or 125 or 126;        // Super/Meta (left, right)
    }

    private void OnError(Exception ex)
    {
        Log.Error(ex, "[GlobalHotkeyService] Error occurred");
    }

    public void SetPlaybackPauseHotkeysEnabled(bool enabled)
    {
        using (_lock.EnterScope())
        {
            _playbackPauseHotkeysEnabled = enabled;
            Log.Information("[GlobalHotkeyService] Playback/Pause hotkeys {Status}", enabled ? "enabled" : "disabled");
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private static HotkeyMapping ParseHotkey(string hotkeyString)
    {
        var mapping = new HotkeyMapping();
        var parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        foreach (var part in parts)
        {
            var keyCode = GetKeyCode(part);
            if (keyCode == -1)
            {
                Log.Warning("[GlobalHotkeyService] Unknown key: {Key}", part);
                continue;
            }

            if (IsModifierKeyCode(keyCode))
            {
                mapping.RequiredModifiers.Add(keyCode);
            }
            else
            {
                mapping.MainKey = keyCode;
            }
        }

        return mapping;
    }

    private static int GetKeyCode(string keyName)
    {
        // Modifier keys
        if (keyName.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
            return 29; // KEY_LEFTCTRL
        if (keyName.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            return 42; // KEY_LEFTSHIFT
        if (keyName.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            return 56; // KEY_LEFTALT
        if (keyName.Equals("Super", StringComparison.OrdinalIgnoreCase) || keyName.Equals("Meta", StringComparison.OrdinalIgnoreCase))
            return 125; // KEY_LEFTMETA

        // Function keys
        if (keyName.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(keyName[1..], out var fNum))
        {
            if (fNum >= 1 && fNum <= 24)
                return 59 + fNum - 1; // F1 = 59, F2 = 60, etc.
        }

        // Letter keys (QWERTY layout mapping)
        if (keyName.Length == 1 && char.IsLetter(keyName[0]))
        {
            return char.ToUpper(keyName[0]) switch
            {
                'Q' => 16, 'W' => 17, 'E' => 18, 'R' => 19, 'T' => 20, 'Y' => 21, 'U' => 22, 'I' => 23, 'O' => 24, 'P' => 25,
                'A' => 30, 'S' => 31, 'D' => 32, 'F' => 33, 'G' => 34, 'H' => 35, 'J' => 36, 'K' => 37, 'L' => 38,
                'Z' => 44, 'X' => 45, 'C' => 46, 'V' => 47, 'B' => 48, 'N' => 49, 'M' => 50,
                _ => -1
            };
        }

        // Number keys (top row)
        if (keyName.Length == 1 && char.IsDigit(keyName[0]))
        {
            var digit = keyName[0] - '0';
            return digit == 0 ? 11 : 2 + digit - 1; // 1 = 2, 2 = 3, ..., 0 = 11
        }

        // Special keys
        return keyName switch
        {
            "Space" => 57,
            "Enter" => 28,
            "Tab" => 15,
            "Backspace" => 14,
            "Escape" or "Esc" => 1,
            "Delete" or "Del" => 111,
            "Insert" or "Ins" => 110,
            "Home" => 102,
            "End" => 107,
            "PageUp" or "PgUp" => 104,
            "PageDown" or "PgDn" => 109,
            "Up" => 103,
            "Down" => 108,
            "Left" => 105,
            "Right" => 106,
            
            "," => 51,
            "." => 52,
            "-" => 12,
            "=" => 13,
            ";" => 39,
            "'" => 40,
            "[" => 26,
            "]" => 27,
            "\\" => 43,
            "/" => 53,
            "`" => 41,
            
            _ => -1
        };
    }

    private class HotkeyMapping
    {
        public int MainKey { get; set; } = -1;
        public HashSet<int> RequiredModifiers { get; set; } = new();
    }
}
