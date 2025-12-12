using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

public class GlobalHotkeyService : IGlobalHotkeyService
{
    private IInputCapture? _inputCapture;
    private bool _isRunning;
    private readonly Lock _lock = new();
    private CancellationTokenSource? _captureCts;

    private readonly IKeyboardLayoutService _layoutService;
    private readonly Func<IInputCapture>? _inputCaptureFactory;
    
    private HotkeyMapping _recordingHotkey = new();
    private HotkeyMapping _playbackHotkey = new();
    private HotkeyMapping _pauseHotkey = new();
    
    private readonly HashSet<int> _pressedModifiers = new();
    
    private readonly Dictionary<string, DateTime> _lastHotkeyPressTimes = new();
    private const int DebounceIntervalMs = 300;
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(DebounceIntervalMs);
    
    private bool _playbackPauseHotkeysEnabled = true;
    
    public event EventHandler? ToggleRecordingRequested;
    public event EventHandler? TogglePlaybackRequested;
    public event EventHandler? TogglePauseRequested;
    
    public int RecordingHotkeyCode => _recordingHotkey.MainKey;
    public int PlaybackHotkeyCode => _playbackHotkey.MainKey;
    public int PauseHotkeyCode => _pauseHotkey.MainKey;
    
    public bool IsRunning => _isRunning;

    private readonly IHotkeyConfigurationService _configService;

    public GlobalHotkeyService(
        IHotkeyConfigurationService configService, 
        IKeyboardLayoutService layoutService,
        Func<IInputCapture>? inputCaptureFactory = null)
    {
        _configService = configService;
        _layoutService = layoutService;
        _inputCaptureFactory = inputCaptureFactory;
        
        var settings = _configService.Load();
        UpdateHotkeys(settings.RecordingHotkey, settings.PlaybackHotkey, settings.PauseHotkey, save: false);
    }

    public void Start()
    {
        using (_lock.EnterScope())
        {
            if (_isRunning) return;

            if (_inputCaptureFactory == null)
            {
                throw new InvalidOperationException("No input capture factory configured");
            }

            _inputCapture = _inputCaptureFactory();
            _inputCapture.Configure(captureMouse: false, captureKeyboard: true);
            _inputCapture.InputReceived += OnInputReceived;
            _inputCapture.Error += OnInputCaptureError;
            
            var devices = _inputCapture.GetAvailableDevices();
            var keyboards = devices.Where(d => d.IsKeyboard).ToList();
            
            if (keyboards.Count == 0)
            {
                _inputCapture.Dispose();
                _inputCapture = null;
                throw new InvalidOperationException("No keyboard devices found");
            }
            
            Log.Information("[GlobalHotkeyService] Found {Count} keyboard device(s):", keyboards.Count);
            foreach (var kbd in keyboards)
            {
                Log.Information("  - {Name} ({Path})", kbd.Name, kbd.Path);
            }
            
            _captureCts = new CancellationTokenSource();
            _ = _inputCapture.StartAsync(_captureCts.Token);
            
            _isRunning = true;
            Log.Information("[GlobalHotkeyService] Started via {ProviderName}", _inputCapture.ProviderName);
        }
    }

    public void Stop()
    {
        using (_lock.EnterScope())
        {
            if (!_isRunning) return;

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
                    Log.Error(ex, "[GlobalHotkeyService] Error stopping input capture");
                }
                _inputCapture = null;
            }
            
            _captureCts?.Cancel();
            _captureCts?.Dispose();
            _captureCts = null;
            
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

    private TaskCompletionSource<string>? _captureTcs;
    private bool _isCapturing;

    public async Task<string> CaptureNextKeyAsync(CancellationToken cancellationToken = default)
    {
        _captureTcs = new TaskCompletionSource<string>();
        _isCapturing = true;
        
        using (_lock.EnterScope())
        {
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

    private void OnInputReceived(object? sender, InputCaptureEventArgs e)
    {
        if (e.Type != InputEventType.Key)
            return;

        using (_lock.EnterScope())
        {
            if (IsModifierKeyCode(e.Code))
            {
                if (e.Value == 1) 
                {
                    _pressedModifiers.Add(e.Code);
                }
                else if (e.Value == 0) 
                {
                    _pressedModifiers.Remove(e.Code);
                }
                return;
            }

            if (e.Value != 1)
                return;

            if (_isCapturing && _captureTcs != null)
            {
                var hotkeyString = BuildHotkeyString(e.Code);
                Task.Run(() => _captureTcs.TrySetResult(hotkeyString));
                return;
            }

            CheckHotkeyMatch(e.Code, "Recording", _recordingHotkey, () => ToggleRecordingRequested?.Invoke(this, EventArgs.Empty));
            
            if (_playbackPauseHotkeysEnabled)
            {
                CheckHotkeyMatch(e.Code, "Playback", _playbackHotkey, () => TogglePlaybackRequested?.Invoke(this, EventArgs.Empty));
                CheckHotkeyMatch(e.Code, "Pause", _pauseHotkey, () => TogglePauseRequested?.Invoke(this, EventArgs.Empty));
            }
        }
    }

    private void OnInputCaptureError(object? sender, string errorMessage)
    {
        Log.Error("[GlobalHotkeyService] Input capture error: {Error}", errorMessage);
    }

    private string BuildHotkeyString(int keyCode)
    {
        var parts = new List<string>();

        if (_pressedModifiers.Contains(29) || _pressedModifiers.Contains(97)) parts.Add("Ctrl");
        if (_pressedModifiers.Contains(42) || _pressedModifiers.Contains(54)) parts.Add("Shift");
        if (_pressedModifiers.Contains(56)) parts.Add("Alt");
        if (_pressedModifiers.Contains(100)) parts.Add("AltGr");
        if (_pressedModifiers.Contains(125) || _pressedModifiers.Contains(126)) parts.Add("Super");

        parts.Add(GetKeyName(keyCode));

        return string.Join("+", parts);
    }

    private string GetKeyName(int keyCode)
    {
         return _layoutService.GetKeyName(keyCode);
    }

    private void CheckHotkeyMatch(int keyCode, string actionName, HotkeyMapping mapping, Action action)
    {
        if (mapping.MainKey != keyCode)
            return;

        if (!mapping.RequiredModifiers.All(m => _pressedModifiers.Contains(m)))
            return;

        if (_pressedModifiers.Except(mapping.RequiredModifiers).Any())
            return;

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
        return code is 29 or 97   
            or 42 or 54           
            or 56 or 100          
            or 125 or 126;        
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

    private HotkeyMapping ParseHotkey(string hotkeyString)
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

    private int GetKeyCode(string keyName)
    {
        if (keyName.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
            return 29; 
        if (keyName.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            return 42; 
        if (keyName.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            return 56; 
        if (keyName.Equals("AltGr", StringComparison.OrdinalIgnoreCase))
            return 100; 
        if (keyName.Equals("Super", StringComparison.OrdinalIgnoreCase) || keyName.Equals("Meta", StringComparison.OrdinalIgnoreCase))
            return 125; 

        if (keyName.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(keyName[1..], out var fNum))
        {
            if (fNum >= 1 && fNum <= 24)
                return 59 + fNum - 1; 
        }

        var special = keyName switch
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
            _ => -1
        };
        if (special != -1) return special;

        for (int i = 0; i < 256; i++)
        {
            var name = GetKeyName(i);
            if (string.Equals(name, keyName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        var code = _layoutService.GetKeyCode(keyName);
        if (code != -1) return code;

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

        if (keyName.Length == 1 && char.IsDigit(keyName[0]))
        {
            var digit = keyName[0] - '0';
            return digit == 0 ? 11 : 2 + digit - 1; 
        }

        return keyName switch
        {
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
