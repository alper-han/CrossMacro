using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Recording tab - handles macro recording functionality
/// </summary>
public class RecordingViewModel : ViewModelBase
{
    private readonly IMacroRecorder _recorder;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISettingsService _settingsService;
    
    private bool _isRecording;
    private int _eventCount;
    private string _recordingStatus = "Ready";
    private bool _isMouseRecordingEnabled = true;
    private bool _isKeyboardRecordingEnabled = true;
    
    /// <summary>
    /// Event fired when recording is completed with the recorded macro
    /// </summary>
    public event EventHandler<MacroSequence>? RecordingCompleted;
    
    /// <summary>
    /// Event fired when recording status changes (for external coordination)
    /// </summary>
    public event EventHandler<bool>? RecordingStateChanged;
    
    public RecordingViewModel(
        IMacroRecorder recorder,
        IGlobalHotkeyService hotkeyService,
        ISettingsService settingsService)
    {
        _recorder = recorder;
        _hotkeyService = hotkeyService;
        _settingsService = settingsService;
        
        // Initialize from saved settings
        _isMouseRecordingEnabled = _settingsService.Current.IsMouseRecordingEnabled;
        _isKeyboardRecordingEnabled = _settingsService.Current.IsKeyboardRecordingEnabled;
        
        // Subscribe to recording events
        _recorder.EventRecorded += OnEventRecorded;
    }
    
    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (_isRecording != value)
            {
                _isRecording = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartRecording));
                RecordingStatus = value ? "Recording..." : "Ready";
                RecordingStateChanged?.Invoke(this, value);
            }
        }
    }
    
    public int EventCount
    {
        get => _eventCount;
        private set
        {
            if (_eventCount != value)
            {
                _eventCount = value;
                OnPropertyChanged();
            }
        }
    }
    
    public string RecordingStatus
    {
        get => _recordingStatus;
        set
        {
            if (_recordingStatus != value)
            {
                _recordingStatus = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool IsMouseRecordingEnabled
    {
        get => _isMouseRecordingEnabled;
        set
        {
            if (_isMouseRecordingEnabled != value)
            {
                _isMouseRecordingEnabled = value;
                _settingsService.Current.IsMouseRecordingEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartRecording));
                _ = _settingsService.SaveAsync();
            }
        }
    }

    public bool IsKeyboardRecordingEnabled
    {
        get => _isKeyboardRecordingEnabled;
        set
        {
            if (_isKeyboardRecordingEnabled != value)
            {
                _isKeyboardRecordingEnabled = value;
                _settingsService.Current.IsKeyboardRecordingEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartRecording));
                _ = _settingsService.SaveAsync();
            }
        }
    }
    
    public bool CanStartRecording => !IsRecording && CanStartRecordingExternal && (IsMouseRecordingEnabled || IsKeyboardRecordingEnabled);
    
    private bool _canStartRecordingExternal = true;
    
    /// <summary>
    /// Used by MainWindowViewModel to control if recording can start (considering playback state)
    /// </summary>
    public bool CanStartRecordingExternal 
    { 
        get => _canStartRecordingExternal;
        set
        {
            if (_canStartRecordingExternal != value)
            {
                _canStartRecordingExternal = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartRecording));
            }
        }
    }
    
    private void OnEventRecorded(object? sender, MacroEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            EventCount++;
        });
    }
    
    public async Task StartRecordingAsync()
    {
        if (!CanStartRecording || !CanStartRecordingExternal)
            return;
            
        try
        {
            IsRecording = true;
            EventCount = 0;
            
            // Disable playback and pause hotkeys during recording so they can be recorded
            _hotkeyService.SetPlaybackPauseHotkeysEnabled(false);
            
            var ignoredKeys = new[] 
            { 
                _hotkeyService.RecordingHotkeyCode,
                _hotkeyService.PlaybackHotkeyCode,
                _hotkeyService.PauseHotkeyCode
            };
            
            await _recorder.StartRecordingAsync(IsMouseRecordingEnabled, IsKeyboardRecordingEnabled, ignoredKeys);
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Error: {ex.Message}";
            IsRecording = false;
            
            // Re-enable hotkeys on error
            _hotkeyService.SetPlaybackPauseHotkeysEnabled(true);
        }
    }
    
    public MacroSequence? StopRecording()
    {
        if (!IsRecording)
            return null;
            
        try
        {
            var macro = _recorder.StopRecording();
            IsRecording = false;
            
            if (macro != null && macro.EventCount > 0)
            {
                RecordingStatus = $"Recorded {macro.EventCount} events";
                RecordingCompleted?.Invoke(this, macro);
                return macro;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Error: {ex.Message}";
            IsRecording = false;
            return null;
        }
        finally
        {
            // Re-enable playback and pause hotkeys after recording stops
            _hotkeyService.SetPlaybackPauseHotkeysEnabled(true);
        }
    }
    
    /// <summary>
    /// Toggle recording state (for hotkey handling)
    /// </summary>
    public void ToggleRecording()
    {
        if (IsRecording)
            StopRecording();
        else if (CanStartRecording && CanStartRecordingExternal)
            _ = StartRecordingAsync();
    }
}
