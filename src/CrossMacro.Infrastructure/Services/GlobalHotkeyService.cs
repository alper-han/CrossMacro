using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Manages global hotkeys for recording, playback, and pause actions.
/// Refactored to delegate responsibilities to specialized services.
/// </summary>
public class GlobalHotkeyService : IGlobalHotkeyService
{
    private IInputCapture? _inputCapture;
    private bool _isRunning;
    private bool _disposed;
    private readonly Lock _lock = new();
    private CancellationTokenSource? _captureCts;
    private int _restartInProgress;

    // Injected services
    private readonly IHotkeyConfigurationService _configService;
    private readonly IHotkeyParser _hotkeyParser;
    private readonly IHotkeyMatcher _hotkeyMatcher;
    private readonly IModifierStateTracker _modifierTracker;
    private readonly IHotkeyStringBuilder _hotkeyStringBuilder;
    private readonly IMouseButtonMapper _mouseButtonMapper;
    private readonly Func<IInputCapture>? _inputCaptureFactory;
    
    // Hotkey mappings
    private HotkeyMapping _recordingHotkey = new();
    private HotkeyMapping _playbackHotkey = new();
    private HotkeyMapping _pauseHotkey = new();
    
    private bool _playbackPauseHotkeysEnabled = true;
    
    // Events
    public event EventHandler? ToggleRecordingRequested;
    public event EventHandler? TogglePlaybackRequested;
    public event EventHandler? TogglePauseRequested;
    public event EventHandler<RawHotkeyInputEventArgs>? RawInputReceived;
    public event EventHandler<RawHotkeyInputEventArgs>? RawKeyReleased;
    public event EventHandler<string>? ErrorOccurred;
    
    // Properties
    public int RecordingHotkeyCode => _recordingHotkey.MainKey;
    public int PlaybackHotkeyCode => _playbackHotkey.MainKey;
    public int PauseHotkeyCode => _pauseHotkey.MainKey;
    public bool IsRunning => _isRunning;
    public string? LastError { get; private set; }

    // Capture mode
    private TaskCompletionSource<string>? _captureTcs;
    private bool _isCapturing;

    public GlobalHotkeyService(
        IHotkeyConfigurationService configService,
        IHotkeyParser hotkeyParser,
        IHotkeyMatcher hotkeyMatcher,
        IModifierStateTracker modifierTracker,
        IHotkeyStringBuilder hotkeyStringBuilder,
        IMouseButtonMapper mouseButtonMapper,
        Func<IInputCapture>? inputCaptureFactory = null)
    {
        _configService = configService;
        _hotkeyParser = hotkeyParser;
        _hotkeyMatcher = hotkeyMatcher;
        _modifierTracker = modifierTracker;
        _hotkeyStringBuilder = hotkeyStringBuilder;
        _mouseButtonMapper = mouseButtonMapper;
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

            StartCapture_NoLock();
            
            _isRunning = true;
            Log.Information("[GlobalHotkeyService] Started via {ProviderName}", _inputCapture?.ProviderName ?? "Unknown");
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
                    StopCapture_NoLock();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[GlobalHotkeyService] Error stopping input capture");
                }
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
            _recordingHotkey = _hotkeyParser.Parse(recordingHotkey);
            _playbackHotkey = _hotkeyParser.Parse(playbackHotkey);
            _pauseHotkey = _hotkeyParser.Parse(pauseHotkey);
            
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

    public async Task<string> CaptureNextKeyAsync(CancellationToken cancellationToken = default)
    {
        _captureTcs = new TaskCompletionSource<string>();
        _isCapturing = true;
        
        _modifierTracker.Clear();

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
        if (e.Type == InputEventType.Key)
        {
            HandleKeyboardInput(e);
            return;
        }
        
        if (e.Type == InputEventType.MouseButton)
        {
            HandleMouseButtonInput(e);
        }
    }
    
    private void HandleKeyboardInput(InputCaptureEventArgs e)
    {
        if (e.Value == 1)
        {
            _modifierTracker.OnKeyPressed(e.Code);
            Log.Debug("[GlobalHotkeyService] Key pressed: Code={Code}, CurrentModifiers=[{Modifiers}]",
                e.Code, string.Join("+", _modifierTracker.CurrentModifiers));
        }
        else if (e.Value == 0)
        {
            var releaseModifiers = _modifierTracker.CurrentModifiers;
            // Always fire RawKeyReleased for all keys (including modifiers)
            // so RunWhileHeld shortcuts can stop when any part of the hotkey is released
            RawKeyReleased?.Invoke(this, new RawHotkeyInputEventArgs(e.Code, releaseModifiers, string.Empty));
            _modifierTracker.OnKeyReleased(e.Code);
        }

        if (e.Value != 1)
            return;

        // Skip if this is a modifier key
        var currentModifiers = _modifierTracker.CurrentModifiers;
        if (currentModifiers.Contains(e.Code))
            return;

        // Block pure mouse left (BTN_LEFT) and right (BTN_RIGHT) clicks without modifiers
        if ((e.Code == InputEventCode.BTN_LEFT || e.Code == InputEventCode.BTN_RIGHT) && !_modifierTracker.HasModifiers)
            return;

        // Build hotkey string
        var hotkeyString = _hotkeyStringBuilder.Build(e.Code, currentModifiers);

        Log.Debug("[GlobalHotkeyService] Hotkey candidate: {HotkeyString} (Code={Code})", hotkeyString, e.Code);

        if (_isCapturing && _captureTcs != null)
        {
            Log.Debug("[GlobalHotkeyService] Captured hotkey: {HotkeyString}", hotkeyString);
            Task.Run(() => _captureTcs.TrySetResult(hotkeyString));
            return;
        }

        // Check hotkey matches
        if (_hotkeyMatcher.TryMatch(e.Code, currentModifiers, _recordingHotkey, "Recording"))
        {
            Log.Information("[GlobalHotkeyService] Recording Hotkey Pressed: {Hotkey}", hotkeyString);
            ToggleRecordingRequested?.Invoke(this, EventArgs.Empty);
        }

        if (_playbackPauseHotkeysEnabled)
        {
            if (_hotkeyMatcher.TryMatch(e.Code, currentModifiers, _playbackHotkey, "Playback"))
            {
                Log.Information("[GlobalHotkeyService] Playback Hotkey Pressed: {Hotkey}", hotkeyString);
                TogglePlaybackRequested?.Invoke(this, EventArgs.Empty);
            }

            if (_hotkeyMatcher.TryMatch(e.Code, currentModifiers, _pauseHotkey, "Pause"))
            {
                Log.Information("[GlobalHotkeyService] Pause Hotkey Pressed: {Hotkey}", hotkeyString);
                TogglePauseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        // Broadcast raw input
        RawInputReceived?.Invoke(this, new RawHotkeyInputEventArgs(e.Code, currentModifiers, hotkeyString));
    }
    
    private void HandleMouseButtonInput(InputCaptureEventArgs e)
    {
        if (e.Value != 1)
            return;
        
        var currentModifiers = _modifierTracker.CurrentModifiers;
        
        // Block pure left/right click without modifiers
        if ((e.Code == InputEventCode.BTN_LEFT || e.Code == InputEventCode.BTN_RIGHT) && !_modifierTracker.HasModifiers)
            return;
        
        var mouseButtonName = _mouseButtonMapper.GetMouseButtonName(e.Code);
        if (string.IsNullOrEmpty(mouseButtonName))
            return;
        
        var hotkeyString = _hotkeyStringBuilder.BuildForMouse(mouseButtonName, currentModifiers);
        
        if (_isCapturing && _captureTcs != null)
        {
            Task.Run(() => _captureTcs.TrySetResult(hotkeyString));
            return;
        }
        
        // Check hotkey matches
        if (_hotkeyMatcher.TryMatch(e.Code, currentModifiers, _recordingHotkey, "Recording"))
        {
            Log.Information("[GlobalHotkeyService] Recording Hotkey Pressed");
            ToggleRecordingRequested?.Invoke(this, EventArgs.Empty);
        }
        
        if (_playbackPauseHotkeysEnabled)
        {
            if (_hotkeyMatcher.TryMatch(e.Code, currentModifiers, _playbackHotkey, "Playback"))
            {
                Log.Information("[GlobalHotkeyService] Playback Hotkey Pressed");
                TogglePlaybackRequested?.Invoke(this, EventArgs.Empty);
            }
            
            if (_hotkeyMatcher.TryMatch(e.Code, currentModifiers, _pauseHotkey, "Pause"))
            {
                Log.Information("[GlobalHotkeyService] Pause Hotkey Pressed");
                TogglePauseRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        
        RawInputReceived?.Invoke(this, new RawHotkeyInputEventArgs(e.Code, currentModifiers, hotkeyString));
    }

    private void OnInputCaptureError(object? sender, string errorMessage)
    {
        Log.Error("[GlobalHotkeyService] Input capture error: {Error}", errorMessage);
        LastError = errorMessage;
        ErrorOccurred?.Invoke(this, errorMessage);

        if (_isRunning)
        {
            _ = TryRestartCaptureAsync(errorMessage);
        }
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    private void StartCapture_NoLock()
    {
        _inputCapture = _inputCaptureFactory!();
        _inputCapture.Configure(captureMouse: true, captureKeyboard: true);
        _inputCapture.InputReceived += OnInputReceived;
        _inputCapture.Error += OnInputCaptureError;

        _captureCts = new CancellationTokenSource();
        _ = _inputCapture.StartAsync(_captureCts.Token);
    }

    private void StopCapture_NoLock()
    {
        if (_inputCapture == null)
        {
            return;
        }

        _inputCapture.InputReceived -= OnInputReceived;
        _inputCapture.Error -= OnInputCaptureError;
        _inputCapture.Stop();
        _inputCapture.Dispose();
        _inputCapture = null;
    }

    private async Task TryRestartCaptureAsync(string cause)
    {
        if (Interlocked.CompareExchange(ref _restartInProgress, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await Task.Delay(250);

            using (_lock.EnterScope())
            {
                if (!_isRunning || _inputCaptureFactory == null)
                {
                    return;
                }

                Log.Warning("[GlobalHotkeyService] Restarting input capture after error: {Cause}", cause);

                try
                {
                    StopCapture_NoLock();
                    _captureCts?.Cancel();
                    _captureCts?.Dispose();
                    _captureCts = null;
                    StartCapture_NoLock();
                    LastError = null;
                }
                catch (Exception ex)
                {
                    LastError = $"Restart failed: {ex.Message}";
                    ErrorOccurred?.Invoke(this, LastError);
                    Log.Error(ex, "[GlobalHotkeyService] Failed to restart input capture");
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _restartInProgress, 0);
        }
    }
}
