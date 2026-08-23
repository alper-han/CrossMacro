
namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Recording tab - handles macro recording functionality
/// </summary>
public partial class RecordingViewModel : ViewModelBase, IDisposable
{
    private enum RecordingStatusKind
    {
        Ready,
        Recording,
        LoadedEvents,
        RecordedEvents,
    }

    private readonly IMacroRecorder _recorder;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly IRuntimeContext _runtimeContext;
    private readonly Action<Action> _postCallback;
    private readonly IMousePositionProvider? _positionProvider;
    private readonly IMousePositionChangeSource? _positionChangeSource;

    private bool _disposed;
    private bool _isStartingRecording;
    private bool _forceRelativeCoordinates;
    private bool _useLogicalRelativeCoordinates;
    private RecordingStatusKind _recordingStatusKind = RecordingStatusKind.Ready;
    private LiveCounterUpdateState? _activeCounterUpdateState;
    private long _nextCounterUpdateSessionId;
    private readonly SettingsSaveRollbackTracker _saveRollbackTracker = new();
    private int _settingsChangeVersion;

    private sealed class LiveCounterUpdateState(long sessionId)
    {
        public long SessionId { get; } = sessionId;

        public long PendingEventCount;
        public long PendingMouseEventCount;
        public long PendingKeyboardEventCount;
        public int IsDrainScheduled;
    }

    [ObservableProperty]
    private string _recordingStatus;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartRecording))]
    [NotifyPropertyChangedFor(nameof(CanToggleRecording))]
    [NotifyCanExecuteChangedFor(nameof(ToggleRecordingCommand))]
    private bool _isMouseRecordingEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartRecording))]
    [NotifyPropertyChangedFor(nameof(CanToggleRecording))]
    [NotifyCanExecuteChangedFor(nameof(ToggleRecordingCommand))]
    private bool _isKeyboardRecordingEnabled;

    [ObservableProperty]
    private bool _skipInitialZeroZero;

    /// <summary>
    /// Used by MainWindowViewModel to control if recording can start (considering playback state)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartRecording))]
    [NotifyPropertyChangedFor(nameof(CanToggleRecording))]
    [NotifyCanExecuteChangedFor(nameof(ToggleRecordingCommand))]
    private bool _canStartRecordingExternal = true;

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
        ILocalizationService localizationService,
        IRuntimeContext runtimeContext,
        IMousePositionProvider? positionProvider = null)
        : this(
            recorder,
            hotkeyService,
            settingsService,
            localizationService,
            runtimeContext,
            action => Dispatcher.UIThread.Post(action),
            positionProvider)
    {
    }

    internal RecordingViewModel(
        IMacroRecorder recorder,
        IGlobalHotkeyService hotkeyService,
        ISettingsService settingsService,
        ILocalizationService localizationService,
        IRuntimeContext runtimeContext,
        Action<Action> postCallback,
        IMousePositionProvider? positionProvider = null)
    {
        ArgumentNullException.ThrowIfNull(postCallback);

        _recorder = recorder;
        _hotkeyService = hotkeyService;
        _settingsService = settingsService;
        _localizationService = localizationService;
        _runtimeContext = runtimeContext;
        _postCallback = postCallback;
        _positionProvider = positionProvider;
        _positionChangeSource = positionProvider as IMousePositionChangeSource;
        _positionChangeSource?.PositionChanged += OnPositionChanged;
        _localizationService.CultureChanged += OnCultureChanged;
        _recordingStatus = BuildRecordingStatus(RecordingStatusKind.Ready);
        _isMouseRecordingEnabled = _settingsService.Current.IsMouseRecordingEnabled;
        _isKeyboardRecordingEnabled = _settingsService.Current.IsKeyboardRecordingEnabled;

        _forceRelativeCoordinates = IsForceRelativeSupported && _settingsService.Current.ForceRelativeCoordinates;
        _useLogicalRelativeCoordinates = _settingsService.Current.UseLogicalRelativeCoordinates;
        _skipInitialZeroZero = _settingsService.Current.SkipInitialZeroZero;

        _recorder.EventRecorded += OnEventRecorded;
    }

    public void RefreshProfileSettings()
    {
        if (IsRecording)
        {
            return;
        }

        // Direct field writes: refreshing from settings must not re-persist via setter hooks.
#pragma warning disable MVVMTK0034
        _isMouseRecordingEnabled = _settingsService.Current.IsMouseRecordingEnabled;
        _isKeyboardRecordingEnabled = _settingsService.Current.IsKeyboardRecordingEnabled;
        _forceRelativeCoordinates = IsForceRelativeSupported && _settingsService.Current.ForceRelativeCoordinates;
        _useLogicalRelativeCoordinates = _settingsService.Current.UseLogicalRelativeCoordinates;
        _skipInitialZeroZero = _settingsService.Current.SkipInitialZeroZero;
#pragma warning restore MVVMTK0034

        OnPropertyChanged(nameof(IsMouseRecordingEnabled));
        OnPropertyChanged(nameof(IsKeyboardRecordingEnabled));
        OnPropertyChanged(nameof(ForceRelativeCoordinates));
        OnPropertyChanged(nameof(UseLogicalRelativeCoordinates));
        OnPropertyChanged(nameof(SkipInitialZeroZero));
        OnPropertyChanged(nameof(ShowLogicalRelativeCoordinatesOption));
        OnPropertyChanged(nameof(IsLogicalRelativeCoordinatesAvailable));
        OnPropertyChanged(nameof(ShowSkipZeroZeroOption));
        OnPropertyChanged(nameof(CanStartRecording));
        OnCanToggleRecordingChanged();
    }

    public bool IsRecording
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartRecording));
                OnCanToggleRecordingChanged();
                SetRecordingStatusKind(value ? RecordingStatusKind.Recording : RecordingStatusKind.Ready);
                RecordingStateChanged?.Invoke(this, value);
            }
        }
    }

    [ObservableProperty]
    public partial int EventCount { get; private set; }

    [ObservableProperty]
    public partial int MouseEventCount { get; private set; }

    [ObservableProperty]
    public partial int KeyboardEventCount { get; private set; }

    partial void OnIsMouseRecordingEnabledChanged(bool oldValue, bool newValue)
    {
        _settingsService.Current.IsMouseRecordingEnabled = newValue;
        _ = TryPersistSettingChange(
            () =>
            {
                _isMouseRecordingEnabled = oldValue;
                _settingsService.Current.IsMouseRecordingEnabled = oldValue;
            },
            nameof(IsMouseRecordingEnabled),
            nameof(CanStartRecording),
            nameof(CanToggleRecording));
    }

    partial void OnIsKeyboardRecordingEnabledChanged(bool oldValue, bool newValue)
    {
        _settingsService.Current.IsKeyboardRecordingEnabled = newValue;
        _ = TryPersistSettingChange(
            () =>
            {
                _isKeyboardRecordingEnabled = oldValue;
                _settingsService.Current.IsKeyboardRecordingEnabled = oldValue;
            },
            nameof(IsKeyboardRecordingEnabled),
            nameof(CanStartRecording),
            nameof(CanToggleRecording));
    }

    public bool ForceRelativeCoordinates
    {
        get => _forceRelativeCoordinates;
        set
        {
            if (value && !IsForceRelativeSupported)
            {
                value = false;
            }

            if (_forceRelativeCoordinates != value)
            {
                var previousValue = _forceRelativeCoordinates;
                _forceRelativeCoordinates = value;
                _settingsService.Current.ForceRelativeCoordinates = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowLogicalRelativeCoordinatesOption));
                OnPropertyChanged(nameof(IsLogicalRelativeCoordinatesAvailable));
                OnPropertyChanged(nameof(ShowSkipZeroZeroOption));
                _ = TryPersistSettingChange(
                    () =>
                    {
                        _forceRelativeCoordinates = previousValue;
                        _settingsService.Current.ForceRelativeCoordinates = previousValue;
                    },
                    nameof(ForceRelativeCoordinates),
                    nameof(ShowLogicalRelativeCoordinatesOption),
                    nameof(IsLogicalRelativeCoordinatesAvailable),
                    nameof(ShowSkipZeroZeroOption));
            }
        }
    }

    public bool IsForceRelativeSupported => _runtimeContext.IsLinux || _runtimeContext.IsWindows || _runtimeContext.IsMacOS;

    public bool UseLogicalRelativeCoordinates
    {
        get => _useLogicalRelativeCoordinates;
        set
        {
            if (_useLogicalRelativeCoordinates == value)
            {
                return;
            }

            var previousValue = _useLogicalRelativeCoordinates;
            _useLogicalRelativeCoordinates = value;
            _settingsService.Current.UseLogicalRelativeCoordinates = value;
            OnPropertyChanged();
            _ = TryPersistSettingChange(
                () =>
                {
                    _useLogicalRelativeCoordinates = previousValue;
                    _settingsService.Current.UseLogicalRelativeCoordinates = previousValue;
                },
                nameof(UseLogicalRelativeCoordinates));
        }
    }

    public bool ShowLogicalRelativeCoordinatesOption => ForceRelativeCoordinates;

    public bool IsLogicalRelativeCoordinatesAvailable => ForceRelativeCoordinates
        && HasGlobalCursorPosition;

    private bool HasGlobalCursorPosition => _positionProvider?.HasUsableAbsolutePosition() is true;

    private void OnPositionChanged(object? sender, MousePositionChangedEventArgs e)
    {
        _postCallback(() =>
        {
            if (!_disposed)
            {
                OnPropertyChanged(nameof(IsLogicalRelativeCoordinatesAvailable));
            }
        });
    }

    partial void OnSkipInitialZeroZeroChanged(bool oldValue, bool newValue)
    {
        _settingsService.Current.SkipInitialZeroZero = newValue;
        _ = TryPersistSettingChange(
            () =>
            {
                _skipInitialZeroZero = oldValue;
                _settingsService.Current.SkipInitialZeroZero = oldValue;
            },
            nameof(SkipInitialZeroZero));
    }

    public bool ShowSkipZeroZeroOption => ForceRelativeCoordinates;

    public bool CanStartRecording => !IsRecording && !_isStartingRecording && CanStartRecordingExternal && (IsMouseRecordingEnabled || IsKeyboardRecordingEnabled);

    /// <summary>
    /// Returns true if the toggle button should be enabled (can start OR can stop)
    /// </summary>
    public bool CanToggleRecording => IsRecording || CanStartRecording;

    private void OnEventRecorded(object? sender, MacroEventRecordedEventArgs e)
    {
        var state = Volatile.Read(ref _activeCounterUpdateState);
        if (state is null || !IsLiveCounterUpdateStateActive(state))
        {
            return;
        }

        AddLiveCounterDelta(state, e.MacroEvent);
        ScheduleLiveCounterDrain(state);
    }

    public async Task StartRecordingAsync()
    {
        if (!CanStartRecording || !CanStartRecordingExternal)
        {
            return;
        }

        try
        {
            await RunOnUiThreadAsync(() => SetRecordingStartupInProgress(value: true)).ConfigureAwait(false);

            // Disable playback and pause hotkeys during recording so they can be recorded
            _hotkeyService.SetPlaybackPauseHotkeysEnabled(enabled: false);

            int[] ignoredKeys =
            [
                _hotkeyService.RecordingHotkeyCode,
                _hotkeyService.PlaybackHotkeyCode,
                _hotkeyService.PauseHotkeyCode,
            ];

            if (ForceRelativeCoordinates && UseLogicalRelativeCoordinates)
            {
                await _recorder.StartRecordingAsync(
                    IsMouseRecordingEnabled,
                    IsKeyboardRecordingEnabled,
                    ignoredKeys,
                    forceRelative: ForceRelativeCoordinates,
                    skipInitialZero: SkipInitialZeroZero,
                    useLogicalRelativeCoordinates: true,
                    cancellationToken: default).ConfigureAwait(false);
            }
            else
            {
                await _recorder.StartRecordingAsync(
                    IsMouseRecordingEnabled,
                    IsKeyboardRecordingEnabled,
                    ignoredKeys,
                    forceRelative: ForceRelativeCoordinates,
                    skipInitialZero: SkipInitialZeroZero,
                    cancellationToken: default).ConfigureAwait(false);
            }

            await RunOnUiThreadAsync(() =>
            {
                IsRecording = true;
                ClearEventCounters();
                ActivateLiveCounterUpdates();
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[RecordingViewModel] StartRecording failed");
            await RunOnUiThreadAsync(() =>
            {
                DeactivateLiveCounterUpdates();
                ClearEventCounters();
                IsRecording = false;
                RecordingStatus = string.Format(
                    _localizationService.CurrentCulture,
                    _localizationService["Recording_StatusError"],
                    ex.Message);
            }).ConfigureAwait(false);

            // Re-enable hotkeys on error
            _hotkeyService.SetPlaybackPauseHotkeysEnabled(enabled: true);
        }
        finally
        {
            await RunOnUiThreadAsync(() => SetRecordingStartupInProgress(value: false)).ConfigureAwait(false);
        }
    }

    public MacroSequence? StopRecording()
    {
        if (!IsRecording)
        {
            return null;
        }

        MacroSequence? macro;
        try
        {
            DeactivateLiveCounterUpdates();
            macro = _recorder.StopRecording();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[RecordingViewModel] StopRecording failed");
            RecordingStatus = string.Format(_localizationService.CurrentCulture, _localizationService["Recording_StatusError"], ex.Message);
            IsRecording = false;
            return null;
        }
        finally
        {
            // Re-enable playback and pause hotkeys after recording stops
            try
            {
                _hotkeyService.SetPlaybackPauseHotkeysEnabled(enabled: true);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[RecordingViewModel] Failed to re-enable playback/pause hotkeys");
            }
        }

        IsRecording = false;

        if (macro is null)
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Keep recording result intact; only downstream synchronization failed.
            Log.LogError(ex, "[RecordingViewModel] RecordingCompleted handler failed");
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
        PostToUiThread(() => RecordingStatus = BuildRecordingStatus(_recordingStatusKind));
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
        if (macro is null)
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
    [RelayCommand(CanExecute = nameof(CanToggleRecording))]
    public void ToggleRecording()
    {
        if (IsRecording)
        {
            _ = StopRecording();
        }
        else if (CanStartRecording && CanStartRecordingExternal)
        {
            _ = StartRecordingAsync();
        }
    }

    private void OnCanToggleRecordingChanged()
    {
        OnPropertyChanged(nameof(CanToggleRecording));
        ToggleRecordingCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DeactivateLiveCounterUpdates();

        // Unsubscribe from events to prevent memory leaks
        _recorder.EventRecorded -= OnEventRecorded;
        _localizationService.CultureChanged -= OnCultureChanged;
        _positionChangeSource?.PositionChanged -= OnPositionChanged;
    }

    private void SetRecordingStatusKind(RecordingStatusKind statusKind)
    {
        _recordingStatusKind = statusKind;
        RecordingStatus = BuildRecordingStatus(statusKind);
    }

    private void SetRecordingStartupInProgress(bool value)
    {
        if (_isStartingRecording == value)
        {
            return;
        }

        _isStartingRecording = value;
        OnPropertyChanged(nameof(CanStartRecording));
        OnCanToggleRecordingChanged();
    }

    private void ActivateLiveCounterUpdates()
    {
        var sessionId = Interlocked.Increment(ref _nextCounterUpdateSessionId);
        Volatile.Write(ref _activeCounterUpdateState, new LiveCounterUpdateState(sessionId: sessionId));
    }

    private void DeactivateLiveCounterUpdates()
    {
        var state = Volatile.Read(ref _activeCounterUpdateState);
        if (state is null)
        {
            return;
        }

        _ = Interlocked.CompareExchange(ref _activeCounterUpdateState, value: null, comparand: state);
    }

    private bool IsLiveCounterUpdateStateActive(LiveCounterUpdateState state)
    {
        return !_disposed &&
            state.SessionId != 0 &&
            ReferenceEquals(state, Volatile.Read(ref _activeCounterUpdateState));
    }

    private static void AddLiveCounterDelta(LiveCounterUpdateState state, MacroEvent macroEvent)
    {
        SaturatingIncrement(ref state.PendingEventCount);

        switch (macroEvent.Type)
        {
            case EventType.ButtonPress:
            case EventType.ButtonRelease:
            case EventType.MouseMove:
            case EventType.Click:
                SaturatingIncrement(ref state.PendingMouseEventCount);
                break;
            case EventType.KeyPress:
            case EventType.KeyRelease:
                SaturatingIncrement(ref state.PendingKeyboardEventCount);
                break;
        }
    }

    private void ScheduleLiveCounterDrain(LiveCounterUpdateState state)
    {
        if (!IsLiveCounterUpdateStateActive(state) ||
            Interlocked.CompareExchange(ref state.IsDrainScheduled, value: 1, comparand: 0) is not 0)
        {
            return;
        }

        try
        {
            _postCallback(() => DrainLiveCounterUpdates(state));
        }
        catch
        {
            Volatile.Write(ref state.IsDrainScheduled, 0);
            throw;
        }
    }

    private void DrainLiveCounterUpdates(LiveCounterUpdateState state)
    {
        if (!IsLiveCounterUpdateStateActive(state))
        {
            return;
        }

        var eventCount = Interlocked.Exchange(ref state.PendingEventCount, 0);
        var mouseEventCount = Interlocked.Exchange(ref state.PendingMouseEventCount, 0);
        var keyboardEventCount = Interlocked.Exchange(ref state.PendingKeyboardEventCount, 0);

        if (!IsLiveCounterUpdateStateActive(state))
        {
            return;
        }

        try
        {
            ApplyLiveCounterUpdate(
                nameof(EventCount),
                () => EventCount = SaturatingAdd(EventCount, eventCount));
            ApplyLiveCounterUpdate(
                nameof(MouseEventCount),
                () => MouseEventCount = SaturatingAdd(MouseEventCount, mouseEventCount));
            ApplyLiveCounterUpdate(
                nameof(KeyboardEventCount),
                () => KeyboardEventCount = SaturatingAdd(KeyboardEventCount, keyboardEventCount));
        }
        finally
        {
            Volatile.Write(ref state.IsDrainScheduled, 0);

            if (Volatile.Read(ref state.PendingEventCount) is not 0 ||
                Volatile.Read(ref state.PendingMouseEventCount) is not 0 ||
                Volatile.Read(ref state.PendingKeyboardEventCount) is not 0)
            {
                ScheduleLiveCounterDrain(state);
            }
        }
    }

    private static void ApplyLiveCounterUpdate(string propertyName, Action update)
    {
        try
        {
            update();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Log.Warning(
                exception,
                "PropertyChanged subscriber failed while updating live counter {PropertyName}",
                propertyName);
        }
    }

    private static void SaturatingIncrement(ref long value)
    {
        while (true)
        {
            var currentValue = Volatile.Read(ref value);
            if (currentValue == long.MaxValue ||
                Interlocked.CompareExchange(ref value, currentValue + 1, currentValue) == currentValue)
            {
                return;
            }
        }
    }

    private static int SaturatingAdd(int value, long delta)
    {
        return delta >= int.MaxValue - (long)value
            ? int.MaxValue
            : value + (int)delta;
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
            _ => _localizationService["Recording_StatusReady"],
        };
    }

    private bool TryPersistSettingChange(Action rollback, params string[] propertyNames)
    {
        var changeVersion = Interlocked.Increment(ref _settingsChangeVersion);
        _ = TryPersistSettingChangeAsync(changeVersion, rollback, propertyNames);
        return true;
    }

    private async Task TryPersistSettingChangeAsync(int changeVersion, Action rollback, string[] propertyNames)
    {
        Task? saveTask = null;

        try
        {
            saveTask = _settingsService.SaveAfterIdleAsync();
            _saveRollbackTracker.Track(saveTask, rollback, propertyNames);
            await saveTask.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var isCoalescedSave = _saveRollbackTracker.TryTakeRollback(
                saveTask,
                propertyNames,
                out var trackedRollback,
                out var isTracked);
            var coalescedRollback = isCoalescedSave ? trackedRollback : null;

            if (coalescedRollback is not null
                || (Volatile.Read(ref _settingsChangeVersion) == changeVersion && !isTracked))
            {
                await RunOnUiThreadAsync(() =>
                {
                    (coalescedRollback ?? rollback)();
                    foreach (var propertyName in propertyNames)
                    {
                        OnPropertyChanged(propertyName);
                    }

                    if (Array.IndexOf(propertyNames, nameof(CanToggleRecording)) >= 0)
                    {
                        ToggleRecordingCommand.NotifyCanExecuteChanged();
                    }
                }).ConfigureAwait(false);
            }

            Log.LogError(ex, "[RecordingViewModel] Failed to persist recording settings");
        }
    }
}
