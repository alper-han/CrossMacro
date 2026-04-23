using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;
using Serilog;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Recording tab - handles macro recording functionality
/// </summary>
public class RecordingViewModel : ViewModelBase, IDisposable
{
    private enum RecordingStatusKind
    {
        Ready,
        Recording,
        LoadedEvents,
        RecordedEvents
    }

    private readonly IMacroRecorder _recorder;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    
    private bool _disposed;
    private bool _isRecording;
    private int _eventCount;
    private int _mouseEventCount;
    private int _keyboardEventCount;
    private string _recordingStatus;
    private bool _isMouseRecordingEnabled = true;
    private bool _isKeyboardRecordingEnabled = true;
    private bool _forceRelativeCoordinates;
    private bool _skipInitialZeroZero;
    private RecordingStatusKind _recordingStatusKind = RecordingStatusKind.Ready;
    private long _activeCounterUpdateSessionId;
    private long _nextCounterUpdateSessionId;
    
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
        ISettingsService settingsService,
        ILocalizationService localizationService)
    {
        _recorder = recorder;
        _hotkeyService = hotkeyService;
        _settingsService = settingsService;
        _localizationService = localizationService;
        _localizationService.CultureChanged += OnCultureChanged;
        _recordingStatus = BuildRecordingStatus(RecordingStatusKind.Ready);
        
        _isKeyboardRecordingEnabled = _settingsService.Current.IsKeyboardRecordingEnabled;
        
        _forceRelativeCoordinates = IsForceRelativeSupported && _settingsService.Current.ForceRelativeCoordinates;
        
        _skipInitialZeroZero = _settingsService.Current.SkipInitialZeroZero;
        
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
                OnPropertyChanged(nameof(CanToggleRecording));
                SetRecordingStatusKind(value ? RecordingStatusKind.Recording : RecordingStatusKind.Ready);
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
    
    public int MouseEventCount
    {
        get => _mouseEventCount;
        private set
        {
            if (_mouseEventCount != value)
            {
                _mouseEventCount = value;
                OnPropertyChanged();
            }
        }
    }
    
    public int KeyboardEventCount
    {
        get => _keyboardEventCount;
        private set
        {
            if (_keyboardEventCount != value)
            {
                _keyboardEventCount = value;
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
                var previousValue = _isMouseRecordingEnabled;
                _isMouseRecordingEnabled = value;
                _settingsService.Current.IsMouseRecordingEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartRecording));
                OnPropertyChanged(nameof(CanToggleRecording));
                TryPersistSettingChange(
                    () =>
                    {
                        _isMouseRecordingEnabled = previousValue;
                        _settingsService.Current.IsMouseRecordingEnabled = previousValue;
                    },
                    nameof(IsMouseRecordingEnabled),
                    nameof(CanStartRecording),
                    nameof(CanToggleRecording));
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
                var previousValue = _isKeyboardRecordingEnabled;
                _isKeyboardRecordingEnabled = value;
                _settingsService.Current.IsKeyboardRecordingEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartRecording));
                OnPropertyChanged(nameof(CanToggleRecording));
                TryPersistSettingChange(
                    () =>
                    {
                        _isKeyboardRecordingEnabled = previousValue;
                        _settingsService.Current.IsKeyboardRecordingEnabled = previousValue;
                    },
                    nameof(IsKeyboardRecordingEnabled),
                    nameof(CanStartRecording),
                    nameof(CanToggleRecording));
            }
        }
    }
    
    public bool ForceRelativeCoordinates
    {
        get => _forceRelativeCoordinates;
        set
        {
            if (value && !IsForceRelativeSupported)
                value = false;

            if (_forceRelativeCoordinates != value)
            {
                var previousValue = _forceRelativeCoordinates;
                _forceRelativeCoordinates = value;
                _settingsService.Current.ForceRelativeCoordinates = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowSkipZeroZeroOption));
                TryPersistSettingChange(
                    () =>
                    {
                        _forceRelativeCoordinates = previousValue;
                        _settingsService.Current.ForceRelativeCoordinates = previousValue;
                    },
                    nameof(ForceRelativeCoordinates),
                    nameof(ShowSkipZeroZeroOption));
            }
        }
    }

    public bool IsForceRelativeSupported => OperatingSystem.IsLinux() || OperatingSystem.IsWindows();
    
    public bool SkipInitialZeroZero
    {
        get => _skipInitialZeroZero;
        set
        {
            if (_skipInitialZeroZero != value)
            {
                var previousValue = _skipInitialZeroZero;
                _skipInitialZeroZero = value;
                _settingsService.Current.SkipInitialZeroZero = value;
                OnPropertyChanged();
                TryPersistSettingChange(
                    () =>
                    {
                        _skipInitialZeroZero = previousValue;
                        _settingsService.Current.SkipInitialZeroZero = previousValue;
                    },
                    nameof(SkipInitialZeroZero));
            }
        }
    }
    
    public bool ShowSkipZeroZeroOption => ForceRelativeCoordinates;
    
    public bool CanStartRecording => !IsRecording && CanStartRecordingExternal && (IsMouseRecordingEnabled || IsKeyboardRecordingEnabled);
    
    /// <summary>
    /// Returns true if the toggle button should be enabled (can start OR can stop)
    /// </summary>
    public bool CanToggleRecording => IsRecording || CanStartRecording;
    
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
                OnPropertyChanged(nameof(CanToggleRecording));
            }
        }
    }
    
    private void OnEventRecorded(object? sender, MacroEvent e)
    {
        var sessionId = Volatile.Read(ref _activeCounterUpdateSessionId);
        if (sessionId == 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            ApplyLiveCounterUpdate(sessionId, e);
        });
    }
    
    public async Task StartRecordingAsync()
    {
        if (!CanStartRecording || !CanStartRecordingExternal)
            return;
            
        try
        {
            ActivateLiveCounterUpdates();
            IsRecording = true;
            ClearEventCounters();
            
            // Disable playback and pause hotkeys during recording so they can be recorded
            _hotkeyService.SetPlaybackPauseHotkeysEnabled(false);
            
            int[] ignoredKeys = 
            [ 
                _hotkeyService.RecordingHotkeyCode,
                _hotkeyService.PlaybackHotkeyCode,
                _hotkeyService.PauseHotkeyCode
            ];
            
            await _recorder.StartRecordingAsync(
                IsMouseRecordingEnabled, 
                IsKeyboardRecordingEnabled, 
                ignoredKeys,
                forceRelative: ForceRelativeCoordinates,
                skipInitialZero: SkipInitialZeroZero);
        }
        catch (Exception ex)
        {
            DeactivateLiveCounterUpdates();
            RecordingStatus = string.Format(_localizationService.CurrentCulture, _localizationService["Recording_StatusError"], ex.Message);
            IsRecording = false;
            
            // Re-enable hotkeys on error
            _hotkeyService.SetPlaybackPauseHotkeysEnabled(true);
        }
    }
    
    public MacroSequence? StopRecording()
    {
        if (!IsRecording)
            return null;

        MacroSequence? macro;
        try
        {
            DeactivateLiveCounterUpdates();
            macro = _recorder.StopRecording();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[RecordingViewModel] StopRecording failed");
            RecordingStatus = string.Format(_localizationService.CurrentCulture, _localizationService["Recording_StatusError"], ex.Message);
            IsRecording = false;
            return null;
        }
        finally
        {
            // Re-enable playback and pause hotkeys after recording stops
            try
            {
                _hotkeyService.SetPlaybackPauseHotkeysEnabled(true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RecordingViewModel] Failed to re-enable playback/pause hotkeys");
            }
        }

        IsRecording = false;

        if (macro == null)
        {
            return null;
        }

        var eventCount = macro.Events?.Count ?? 0;
        if (eventCount <= 0)
        {
            return null;
        }

        ApplyEventCounters(macro.Events!);
        SetRecordingStatusKind(RecordingStatusKind.RecordedEvents);

        try
        {
            RecordingCompleted?.Invoke(this, macro);
        }
        catch (Exception ex)
        {
            // Keep recording result intact; only downstream synchronization failed.
            Log.Error(ex, "[RecordingViewModel] RecordingCompleted handler failed");
        }

        return macro;
    }
    
    private void ClearEventCounters()
    {
        EventCount = 0;
        MouseEventCount = 0;
        KeyboardEventCount = 0;
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        RecordingStatus = BuildRecordingStatus(_recordingStatusKind);
    }

    private void ApplyEventCounters(IEnumerable<MacroEvent> events)
    {
        var totalCount = 0;
        var mouseCount = 0;
        var keyboardCount = 0;

        foreach (var e in events)
        {
            totalCount++;

            switch (e.Type)
            {
                case EventType.ButtonPress:
                case EventType.ButtonRelease:
                case EventType.MouseMove:
                case EventType.Click:
                    mouseCount++;
                    break;
                case EventType.KeyPress:
                case EventType.KeyRelease:
                    keyboardCount++;
                    break;
            }
        }

        EventCount = totalCount;
        MouseEventCount = mouseCount;
        KeyboardEventCount = keyboardCount;
    }

    /// <summary>
    /// Set the current macro summary (called when loading from file or changing loaded selection).
    /// </summary>
    public void SetMacro(MacroSequence? macro, bool updateStatus = true)
    {
        if (macro == null)
        {
            ClearEventCounters();
            if (updateStatus)
            {
                SetRecordingStatusKind(RecordingStatusKind.Ready);
            }

            return;
        }

        IEnumerable<MacroEvent> events = macro.Events is null
            ? Array.Empty<MacroEvent>()
            : macro.Events;
        ApplyEventCounters(events);

        if (updateStatus)
        {
            SetRecordingStatusKind(RecordingStatusKind.LoadedEvents);
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
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        // Unsubscribe from events to prevent memory leaks
        _recorder.EventRecorded -= OnEventRecorded;
        _localizationService.CultureChanged -= OnCultureChanged;
    }

    private void SetRecordingStatusKind(RecordingStatusKind statusKind)
    {
        _recordingStatusKind = statusKind;
        RecordingStatus = BuildRecordingStatus(statusKind);
    }

    private void ActivateLiveCounterUpdates()
    {
        var sessionId = Interlocked.Increment(ref _nextCounterUpdateSessionId);
        Volatile.Write(ref _activeCounterUpdateSessionId, sessionId);
    }

    private void DeactivateLiveCounterUpdates()
    {
        Volatile.Write(ref _activeCounterUpdateSessionId, 0);
    }

    private void ApplyLiveCounterUpdate(long sessionId, MacroEvent macroEvent)
    {
        if (sessionId == 0 || sessionId != Volatile.Read(ref _activeCounterUpdateSessionId))
        {
            return;
        }

        EventCount++;

        // Track mouse and keyboard events separately
        switch (macroEvent.Type)
        {
            case EventType.ButtonPress:
            case EventType.ButtonRelease:
            case EventType.MouseMove:
            case EventType.Click:
                MouseEventCount++;
                break;
            case EventType.KeyPress:
            case EventType.KeyRelease:
                KeyboardEventCount++;
                break;
        }
    }

    private string BuildRecordingStatus(RecordingStatusKind statusKind)
    {
        return statusKind switch
        {
            RecordingStatusKind.Ready => _localizationService["Recording_StatusReady"],
            RecordingStatusKind.Recording => _localizationService["Recording_StatusRecording"],
            RecordingStatusKind.LoadedEvents => string.Format(
                _localizationService.CurrentCulture,
                _localizationService["Recording_StatusLoadedEvents"],
                EventCount),
            RecordingStatusKind.RecordedEvents => string.Format(
                _localizationService.CurrentCulture,
                _localizationService["Recording_StatusRecordedEvents"],
                EventCount),
            _ => _localizationService["Recording_StatusReady"]
        };
    }

    private bool TryPersistSettingChange(Action rollback, params string[] propertyNames)
    {
        try
        {
            _settingsService.Save();
            return true;
        }
        catch (Exception ex)
        {
            rollback();
            foreach (var propertyName in propertyNames)
            {
                OnPropertyChanged(propertyName);
            }

            Log.Error(ex, "[RecordingViewModel] Failed to persist recording settings");
            return false;
        }
    }
}
