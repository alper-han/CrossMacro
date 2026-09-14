
namespace CrossMacro.UI.ViewModels.Playback;

/// <summary>
/// ViewModel for the Playback tab - handles macro playback functionality
/// </summary>
public partial class PlaybackViewModel : ViewModelBase, IDisposable
{
    // External snapshots update presentation without re-running user-change effects.
    private bool _isRefreshingPresentation;

    private const int FastLoopWarningThresholdMs = 100;

    private bool _disposed;

    private readonly IMacroPlayer _player;
    private readonly ISettingsService _settingsService;
    private readonly ILoadedMacroSession _loadedMacroSession;
    private readonly ILocalizationService _localizationService;
    private readonly IDialogService? _dialogService;
    private readonly Func<int, int, int> _randomInclusive;

    private double _playbackSpeed = 1.0;
    private MotionPlaybackMode _motionPlaybackMode = MotionPlaybackMode.Precision;
    private int? _precisionMotionEventsPerSecond = PlaybackOptions.DefaultPrecisionMotionEventsPerSecond;
    private int? _strictSpeedMotionEventsPerSecond = PlaybackOptions.DefaultStrictSpeedMotionEventsPerSecond;
    private int? _loopDelayMs = 0;
    private int? _loopDelayMinMs = 0;
    private int? _loopDelayMaxMs = 0;
    private string _playbackStatus;
    private int _stopRequested;
    private int _settingsChangeVersion;
    private bool _fastLoopWarningAcknowledged;
    private Task? _fastLoopWarningTask;
    private double _maximumMotionErrorPixels = PlaybackOptions.DefaultMaximumMotionErrorPixels;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFixedLoopDelayInput))]
    [NotifyPropertyChangedFor(nameof(ShowRandomLoopDelayInputs))]
    private bool _isLooping;

    [ObservableProperty]
    private int _loopCount = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoopDelayMinMs))]
    [NotifyPropertyChangedFor(nameof(LoopDelayMaxMs))]
    [NotifyPropertyChangedFor(nameof(ShowFixedLoopDelayInput))]
    [NotifyPropertyChangedFor(nameof(ShowRandomLoopDelayInputs))]
    private bool _useRandomLoopDelay;

    [ObservableProperty]
    private int? _countdownSeconds = 0;

    /// <summary>
    /// Used by MainWindowViewModel to control if playback can start (considering recording state)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPlayMacro))]
    private bool _canPlayMacroExternal = true;
    private bool _isSequencePlayback;
    private bool _isWaitingBetweenSequenceCycles;
    private int _sequenceMacroIndex;
    private int _sequenceMacroCount;
    private int _sequenceCycle;
    private int _sequenceTotalCycles;
    private string _sequenceMacroName = string.Empty;
    private int _sequenceMacroRepeatCount = 1;
    private readonly SettingsChangeCoordinator _settingsChanges;
    private AppSettings _settingsDraft;
    private AppSettings _lastSubmittedSettings;

    private MacroSequence? _currentMacro;
    private readonly Lock _playbackGate = new();
    private PlaybackSession? _playbackSession;
    private DispatcherTimer? _statusUpdateTimer;

    private bool StopRequested => Volatile.Read(ref _stopRequested) is not 0;

    /// <summary>
    /// Event fired when playback state changes
    /// </summary>
    public event EventHandler<bool>? PlaybackStateChanged;

    /// <summary>
    /// Event fired when status message changes
    /// </summary>
    public event EventHandler<string>? StatusChanged;

    public PlaybackViewModel(
        IMacroPlayer player,
        ISettingsService settingsService,
        ILoadedMacroSession loadedMacroSession,
        ILocalizationService? localizationService = null,
        IDialogService? dialogService = null,
        IUiDispatcher? uiDispatcher = null,
        SettingsChangeCoordinator? settingsChanges = null)
        : this(player, settingsService, loadedMacroSession, localizationService, dialogService, RandomNumberGeneratorUtility.GetInt32Inclusive, uiDispatcher: uiDispatcher, settingsChanges: settingsChanges) { /* Empty */ }

    internal PlaybackViewModel(
        IMacroPlayer player,
        ISettingsService settingsService,
        ILoadedMacroSession loadedMacroSession,
        ILocalizationService? localizationService,
        IDialogService? dialogService,
        Func<int, int, int> randomInclusive,
        IUiDispatcher? uiDispatcher = null,
        SettingsChangeCoordinator? settingsChanges = null)
        : base(uiDispatcher)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _settingsChanges = settingsChanges ?? new SettingsChangeCoordinator(settingsService);
        _settingsDraft = AppSettingsSnapshot.Copy(settingsService.Current);
        _lastSubmittedSettings = AppSettingsSnapshot.Copy(_settingsDraft);
        _loadedMacroSession = loadedMacroSession ?? throw new ArgumentNullException(nameof(loadedMacroSession));
        _localizationService = localizationService ?? new LocalizationService();
        _dialogService = dialogService;
        _randomInclusive = randomInclusive ?? throw new ArgumentNullException(nameof(randomInclusive));
        _playbackStatus = _localizationService["Playback_StatusReady"];

        // Initialize playback settings from saved settings
        _playbackSpeed = _settingsDraft.PlaybackSpeed;
        _motionPlaybackMode = _settingsDraft.MotionMode;
        _precisionMotionEventsPerSecond = _settingsDraft.PrecisionMotionEventsPerSecond;
        _strictSpeedMotionEventsPerSecond = _settingsDraft.StrictSpeedMotionEventsPerSecond;
        _maximumMotionErrorPixels = _settingsDraft.MaximumMotionErrorPixels;
        _isLooping = _settingsDraft.IsLooping;
        _loopCount = _settingsDraft.LoopCount;
        _loopDelayMs = _settingsDraft.LoopDelayMs;
        _useRandomLoopDelay = _settingsDraft.UseRandomLoopDelay;
        _loopDelayMinMs = _settingsDraft.LoopDelayMinMs;
        _loopDelayMaxMs = _settingsDraft.LoopDelayMaxMs;
        _countdownSeconds = _settingsDraft.CountdownSeconds;
        _currentMacro = _loadedMacroSession.SelectedMacro;

        _loadedMacroSession.SelectedMacroChanged += OnLoadedMacroSelectionChanged;
        _loadedMacroSession.SelectedMacroUpdated += OnLoadedMacroUpdated;
        _loadedMacroSession.PlaybackModeChanged += OnLoadedMacroPlaybackModeChanged;
        _localizationService.CultureChanged += OnCultureChanged;

        // Setup status update timer
        _statusUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _statusUpdateTimer.Tick += OnStatusUpdateTimerTick;
    }

    public void RefreshProfileSettings()
    {
        if (IsPlaying)
        {
            return;
        }

        RefreshSettingsPresentation();
    }

    private void RefreshSettingsPresentation()
    {
        _settingsDraft = AppSettingsSnapshot.Copy(_settingsService.Current);
        _lastSubmittedSettings = AppSettingsSnapshot.Copy(_settingsDraft);
        // Apply the external snapshot without persisting it as a user edit.
        var wasRefreshingPresentation = _isRefreshingPresentation;
        _isRefreshingPresentation = true;
        try
        {
            _playbackSpeed = _settingsDraft.PlaybackSpeed;
            _motionPlaybackMode = _settingsDraft.MotionMode;
            _precisionMotionEventsPerSecond = _settingsDraft.PrecisionMotionEventsPerSecond;
            _strictSpeedMotionEventsPerSecond = _settingsDraft.StrictSpeedMotionEventsPerSecond;
            _maximumMotionErrorPixels = _settingsDraft.MaximumMotionErrorPixels;
            IsLooping = _settingsDraft.IsLooping;
            LoopCount = _settingsDraft.LoopCount;
            _loopDelayMs = _settingsDraft.LoopDelayMs;
            UseRandomLoopDelay = _settingsDraft.UseRandomLoopDelay;
            _loopDelayMinMs = _settingsDraft.LoopDelayMinMs;
            _loopDelayMaxMs = _settingsDraft.LoopDelayMaxMs;
            CountdownSeconds = _settingsDraft.CountdownSeconds;
        }
        finally
        {
            _isRefreshingPresentation = wasRefreshingPresentation;
        }

        OnPropertyChanged(nameof(PlaybackSpeed));
        OnPropertyChanged(nameof(MotionPlaybackMode));
        OnPropertyChanged(nameof(PrecisionMotionEventsPerSecond));
        OnPropertyChanged(nameof(StrictSpeedMotionEventsPerSecond));
        OnPropertyChanged(nameof(MaximumMotionErrorPixels));
        OnPropertyChanged(nameof(IsPrecisionMotionMode));
        OnPropertyChanged(nameof(IsStrictSpeedMotionMode));
        OnPropertyChanged(nameof(ShowPrecisionMotionRate));
        OnPropertyChanged(nameof(ShowStrictSpeedMotionRate));
        OnPropertyChanged(nameof(IsLooping));
        OnPropertyChanged(nameof(LoopCount));
        OnPropertyChanged(nameof(LoopDelayMs));
        OnPropertyChanged(nameof(UseRandomLoopDelay));
        OnPropertyChanged(nameof(LoopDelayMinMs));
        OnPropertyChanged(nameof(LoopDelayMaxMs));
        OnPropertyChanged(nameof(CountdownSeconds));
        OnPropertyChanged(nameof(CanPlayMacro));
    }

    private void OnStatusUpdateTimerTick(object? sender, EventArgs e)
    {
        if (IsPlaying && !IsPaused && !StopRequested)
        {
            ApplyPlaybackStatus();
        }
    }

    private void ApplyPlaybackStatus()
    {
        if (_isSequencePlayback)
        {
            if (_isWaitingBetweenSequenceCycles)
            {
                PlaybackStatus = string.Format(_localizationService.CurrentCulture, _localizationService["Playback_StatusWaitingNextSequence"], GetLoopDelayWaitText());
                return;
            }

            var macroName = string.IsNullOrWhiteSpace(_sequenceMacroName)
                ? _localizationService["Playback_UnnamedMacro"]
                : _sequenceMacroName;
            var macroIndex = Math.Max(1, _sequenceMacroIndex);
            var macroCount = Math.Max(1, _sequenceMacroCount);
            var cycleText = _sequenceTotalCycles is 0
                ? string.Format(
                    _localizationService.CurrentCulture,
                    _localizationService["Playback_SequenceCycleInfinite"],
                    Math.Max(1, _sequenceCycle))
                : $"{Math.Max(1, _sequenceCycle).ToString(CultureInfo.InvariantCulture)}/{Math.Max(1, _sequenceTotalCycles).ToString(CultureInfo.InvariantCulture)}";
            var repeatCount = Math.Max(1, _sequenceMacroRepeatCount);
            var repeatText = string.Empty;

            if (repeatCount > 1)
            {
                var currentRepeat = _player.TotalLoops == repeatCount
                    ? Math.Clamp(Math.Max(1, _player.CurrentLoop), 1, repeatCount)
                    : 1;
                repeatText = string.Format(
                    _localizationService.CurrentCulture,
                    _localizationService["Playback_SequenceRepeatProgress"],
                    currentRepeat,
                    repeatCount);
            }

            PlaybackStatus = string.Format(_localizationService.CurrentCulture, _localizationService["Playback_StatusSequencePlaying"], macroName, macroIndex, macroCount, repeatText, cycleText);
            return;
        }

        var currentLoop = _player.CurrentLoop;
        var totalLoops = _player.TotalLoops;
        var isWaiting = _player.IsWaitingBetweenLoops;

        if (isWaiting)
        {
            PlaybackStatus = string.Format(_localizationService.CurrentCulture, _localizationService["Playback_StatusWaitingNextLoop"], GetLoopDelayWaitText());
            return;
        }

        if (totalLoops is 0)
        {
            PlaybackStatus = string.Format(_localizationService.CurrentCulture, _localizationService["Playback_StatusLoopInfinite"], currentLoop);
        }
        else if (totalLoops > 1)
        {
            PlaybackStatus = string.Format(_localizationService.CurrentCulture, _localizationService["Playback_StatusLoopProgress"], currentLoop, totalLoops);
        }
        else
        {
            PlaybackStatus = _localizationService["Playback_StatusPlaying"];
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        PostToUiThread(() =>
        {
            if (IsPlaying && !IsPaused && !StopRequested)
            {
                ApplyPlaybackStatus();
                return;
            }

            if (IsPaused)
            {
                PlaybackStatus = _localizationService["Playback_StatusPaused"];
                return;
            }

            if (StopRequested)
            {
                PlaybackStatus = _localizationService["Playback_StatusStopped"];
                return;
            }

            PlaybackStatus = _localizationService["Playback_StatusReady"];
        });
    }

    // Kept manual: normalizes (coerces) the incoming value and compares with an epsilon instead of equality.
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            var normalized = PlaybackOptions.NormalizeSpeedMultiplier(value);
            if (Math.Abs(_playbackSpeed - normalized) > 0.01)
            {
                _playbackSpeed = normalized;
                _settingsDraft.PlaybackSpeed = normalized;
                OnPropertyChanged();
                _ = TryPersistSettingChange(nameof(PlaybackSpeed));
            }
        }
    }

    public MotionPlaybackMode MotionPlaybackMode
    {
        get => _motionPlaybackMode;
        set
        {
            var normalized = Enum.IsDefined(value) ? value : MotionPlaybackMode.Precision;
            if (_motionPlaybackMode == normalized)
            {
                return;
            }

            _motionPlaybackMode = normalized;
            _settingsDraft.MotionMode = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPrecisionMotionMode));
            OnPropertyChanged(nameof(IsStrictSpeedMotionMode));
            OnPropertyChanged(nameof(ShowPrecisionMotionRate));
            OnPropertyChanged(nameof(ShowStrictSpeedMotionRate));
            _ = TryPersistSettingChange(nameof(MotionPlaybackMode),
                nameof(IsPrecisionMotionMode),
                nameof(IsStrictSpeedMotionMode),
                nameof(ShowPrecisionMotionRate),
                nameof(ShowStrictSpeedMotionRate));
        }
    }

    public bool IsPrecisionMotionMode
    {
        get => MotionPlaybackMode is MotionPlaybackMode.Precision;
        set
        {
            if (value)
            {
                MotionPlaybackMode = MotionPlaybackMode.Precision;
            }
        }
    }

    public bool IsStrictSpeedMotionMode
    {
        get => MotionPlaybackMode is MotionPlaybackMode.StrictSpeed;
        set
        {
            if (value)
            {
                MotionPlaybackMode = MotionPlaybackMode.StrictSpeed;
            }
        }
    }

    public bool ShowPrecisionMotionRate => IsPrecisionMotionMode;

    public bool ShowStrictSpeedMotionRate => IsStrictSpeedMotionMode;

    public int? PrecisionMotionEventsPerSecond
    {
        get => _precisionMotionEventsPerSecond;
        set
        {
            var normalized = PlaybackOptions.NormalizePrecisionMotionEventsPerSecond(value ?? 0);
            if (_precisionMotionEventsPerSecond == normalized)
            {
                return;
            }

            _precisionMotionEventsPerSecond = normalized;
            _settingsDraft.PrecisionMotionEventsPerSecond = normalized;
            OnPropertyChanged();
            _ = TryPersistSettingChange(nameof(PrecisionMotionEventsPerSecond));
        }
    }

    public int? StrictSpeedMotionEventsPerSecond
    {
        get => _strictSpeedMotionEventsPerSecond;
        set
        {
            var normalized = PlaybackOptions.NormalizeStrictSpeedMotionEventsPerSecond(value ?? 0);
            if (_strictSpeedMotionEventsPerSecond == normalized)
            {
                return;
            }

            _strictSpeedMotionEventsPerSecond = normalized;
            _settingsDraft.StrictSpeedMotionEventsPerSecond = normalized;
            OnPropertyChanged();
            _ = TryPersistSettingChange(nameof(StrictSpeedMotionEventsPerSecond));
        }
    }

    public double MaximumMotionErrorPixels
    {
        get => _maximumMotionErrorPixels;
        set
        {
            var normalized = PlaybackOptions.NormalizeMaximumMotionErrorPixels(value);
            if (Math.Abs(_maximumMotionErrorPixels - normalized) < 0.001d)
            {
                return;
            }

            _maximumMotionErrorPixels = normalized;
            _settingsDraft.MaximumMotionErrorPixels = normalized;
            OnPropertyChanged();
            _ = TryPersistSettingChange(nameof(MaximumMotionErrorPixels));
        }
    }

    partial void OnIsLoopingChanged(bool oldValue, bool newValue)
    {
        if (_isRefreshingPresentation) { return; }
        _settingsDraft.IsLooping = newValue;
        PersistLoopSettingChange(
            () =>
            {
                _isLooping = oldValue;
                _settingsDraft.IsLooping = oldValue;
            },
            nameof(IsLooping),
            nameof(ShowFixedLoopDelayInput),
            nameof(ShowRandomLoopDelayInputs));
    }

    partial void OnLoopCountChanged(int oldValue, int newValue)
    {
        if (_isRefreshingPresentation) { return; }
        _settingsDraft.LoopCount = newValue;
        PersistLoopSettingChange(
            () =>
            {
                _loopCount = oldValue;
                _settingsDraft.LoopCount = oldValue;
            },
            nameof(LoopCount));
    }

    // Kept manual: normalizes (coerces) the incoming value before the change check.
    public int? LoopDelayMs
    {
        get => _loopDelayMs;
        set
        {
            var normalized = NormalizeDelayInput(value);
            if (_loopDelayMs != normalized)
            {
                var previousValue = _loopDelayMs ?? 0;
                _loopDelayMs = normalized;
                _settingsDraft.LoopDelayMs = normalized;
                OnPropertyChanged();
                PersistLoopSettingChange(
                    () =>
                    {
                        _loopDelayMs = previousValue;
                        _settingsDraft.LoopDelayMs = previousValue;
                    },
                    nameof(LoopDelayMs));
            }
        }
    }

    partial void OnUseRandomLoopDelayChanged(bool oldValue, bool newValue)
    {
        if (_isRefreshingPresentation) { return; }
        var previousMin = _loopDelayMinMs ?? 0;
        var previousMax = _loopDelayMaxMs ?? 0;

        if (newValue && previousMin is 0 && previousMax is 0)
        {
            var seededDelay = NormalizeDelayInput(LoopDelayMs);
            UpdateLoopDelayRange(seededDelay, seededDelay);
        }

        _settingsDraft.UseRandomLoopDelay = newValue;

        PersistLoopSettingChange(
            () =>
            {
                _useRandomLoopDelay = oldValue;
                UpdateLoopDelayRange(previousMin, previousMax);
                _settingsDraft.UseRandomLoopDelay = oldValue;
            },
            nameof(UseRandomLoopDelay),
            nameof(LoopDelayMinMs),
            nameof(LoopDelayMaxMs),
            nameof(ShowFixedLoopDelayInput),
            nameof(ShowRandomLoopDelayInputs));
    }

    // Kept manual: cross-property coercion (NormalizeDelayRange) with a conditional partner notification.
    public int? LoopDelayMinMs
    {
        get => _loopDelayMinMs;
        set
        {
            var previousMin = _loopDelayMinMs ?? 0;
            var previousMax = _loopDelayMaxMs ?? 0;
            var (normalizedMin, normalizedMax) = PlaybackOptions.NormalizeDelayRange(value ?? 0, previousMax);
            if (_loopDelayMinMs == normalizedMin && _loopDelayMaxMs == normalizedMax)
            {
                return;
            }

            _loopDelayMinMs = normalizedMin;
            _loopDelayMaxMs = normalizedMax;
            _settingsDraft.LoopDelayMinMs = normalizedMin;
            _settingsDraft.LoopDelayMaxMs = normalizedMax;
            OnPropertyChanged();
            if (previousMax != normalizedMax)
            {
                OnPropertyChanged(nameof(LoopDelayMaxMs));
            }

            PersistLoopSettingChange(
                () => UpdateLoopDelayRange(previousMin, previousMax),
                nameof(LoopDelayMinMs),
                nameof(LoopDelayMaxMs));
        }
    }

    // Kept manual: cross-property coercion (NormalizeDelayRange) with a conditional partner notification.
    public int? LoopDelayMaxMs
    {
        get => _loopDelayMaxMs;
        set
        {
            var previousMin = _loopDelayMinMs ?? 0;
            var previousMax = _loopDelayMaxMs ?? 0;
            var (normalizedMin, normalizedMax) = PlaybackOptions.NormalizeDelayRange(previousMin, value ?? 0);
            if (_loopDelayMinMs == normalizedMin && _loopDelayMaxMs == normalizedMax)
            {
                return;
            }

            _loopDelayMinMs = normalizedMin;
            _loopDelayMaxMs = normalizedMax;
            _settingsDraft.LoopDelayMinMs = normalizedMin;
            _settingsDraft.LoopDelayMaxMs = normalizedMax;
            OnPropertyChanged();
            if (previousMin != normalizedMin)
            {
                OnPropertyChanged(nameof(LoopDelayMinMs));
            }

            PersistLoopSettingChange(
                () => UpdateLoopDelayRange(previousMin, previousMax),
                nameof(LoopDelayMinMs),
                nameof(LoopDelayMaxMs));
        }
    }

    public bool ShowFixedLoopDelayInput => IsLooping && !UseRandomLoopDelay;

    public bool ShowRandomLoopDelayInputs => IsLooping && UseRandomLoopDelay;

    partial void OnCountdownSecondsChanged(int? oldValue, int? newValue)
    {
        if (_isRefreshingPresentation) { return; }
        _settingsDraft.CountdownSeconds = newValue ?? 0;
        _ = TryPersistSettingChange(nameof(CountdownSeconds));
    }

    // Kept manual: PlaybackStateChanged must fire after the CanPlayMacro notification, a generated OnChanged hook would fire before it.
    public bool IsPlaying
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanPlayMacro));
                PlaybackStateChanged?.Invoke(this, value);
            }
        }
    }

    [ObservableProperty]
    public partial bool IsPaused { get; private set; }

    // Kept manual: StatusChanged must fire after the PropertyChanged notification, a generated OnChanged hook would fire before it.
    public string PlaybackStatus
    {
        get => _playbackStatus;
        private set
        {
            if (!string.Equals(_playbackStatus, value, StringComparison.Ordinal))
            {
                _playbackStatus = value;
                OnPropertyChanged();
                StatusChanged?.Invoke(this, value);
            }
        }
    }

    public bool HasMacro => PlaybackExecutionPlanner.HasPlayableEvents(
        PlaybackExecutionPlanner.GetPreviewMacro(_loadedMacroSession, _currentMacro));

    public bool CanPlayMacro => HasMacro && !IsPlaying && CanPlayMacroExternal;

    /// <summary>
    /// Set the fallback macro to be played.
    /// Session-backed selection takes precedence when present.
    /// </summary>
    public void SetMacro(MacroSequence? macro)
    {
        _currentMacro = macro;
        OnPropertyChanged(nameof(HasMacro));
        OnPropertyChanged(nameof(CanPlayMacro));
    }

    public async Task PlayMacroAsync()
    {
        PlaybackSession session;
        lock (_playbackGate)
        {
            if (_disposed || _playbackSession is not null || IsPlaying || !CanPlayMacroExternal)
            {
                return;
            }
            session = new PlaybackSession();
            _playbackSession = session;
            Volatile.Write(ref _stopRequested, 0);
        }

        try
        {
            await PlayMacroCoreAsync(session).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (session.IsStopRequested)
        {
            // Stop or disposal can cancel a request while its approval is still open.
        }
        finally
        {
            try
            {
                await UiDispatcher.InvokeAsync(() =>
                {
                    lock (_playbackGate)
                    {
                        if (!ReferenceEquals(_playbackSession, session)) { return; }
                        if (!_disposed)
                        {
                            _statusUpdateTimer?.Stop();
                            ResetSequenceState();
                            IsPlaying = false;
                            IsPaused = false;
                        }
                        _playbackSession = null;
                    }
                }).ConfigureAwait(false);
            }
            finally { session.Dispose(); }
        }
    }

    private async Task PlayMacroCoreAsync(PlaybackSession session)
    {
        var executionPlan = PlaybackExecutionPlanner.CreatePlan(_loadedMacroSession, _currentMacro);
        if (!string.IsNullOrEmpty(executionPlan.ValidationError))
        {
            await UiDispatcher.InvokeAsync(() =>
            {
                PlaybackStatus = executionPlan.ValidationError;
                return Task.CompletedTask;
            }).ConfigureAwait(false);
            return;
        }

        if (!PlaybackExecutionPlanner.HasPlayableEvents(executionPlan.ActiveMacro))
        {
            return;
        }

        if (!await ConfirmFastLoopPlaybackAsync(forPlayback: true).WaitAsync(session.Token).ConfigureAwait(false))
        {
            return;
        }

        var playbackMode = executionPlan.Mode;
        var activeMacro = executionPlan.ActiveMacro!;
        var sequenceSnapshot = executionPlan.SequenceSnapshot;

        session.Token.ThrowIfCancellationRequested();
        if (!CanPlayMacroExternal) { return; }
        var completedNormally = false;

        try
        {
            await UiDispatcher.InvokeAsync(() =>
            {
                lock (_playbackGate)
                {
                    session.Token.ThrowIfCancellationRequested();
                    if (_disposed || !CanPlayMacroExternal) { session.RequestStop(_player); return Task.CompletedTask; }
                    ResetSequenceState();
                    IsPlaying = true;
                    IsPaused = false;
                }
                return Task.CompletedTask;
            }).ConfigureAwait(false);

            await WaitForCountdownAsync(session.Token).ConfigureAwait(false);
            if (StopRequested)
            {
                return;
            }

            await UiDispatcher.InvokeAsync(() =>
            {
                _statusUpdateTimer?.Start();
                return Task.CompletedTask;
            }).ConfigureAwait(false);

            if (executionPlan.UsesSequence)
            {
                await PlaySequentialCycleAsync(session, sequenceSnapshot, session.Token).ConfigureAwait(false);
            }
            else
            {
                await PlaySingleMacroModeAsync(session, activeMacro, playbackMode).ConfigureAwait(false);
            }

            completedNormally = !StopRequested;
        }
        catch (OperationCanceledException) when (session.IsStopRequested) { /* Empty */ }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (ex is AbsolutePlaybackUnsupportedException)
            {
                await UiDispatcher.InvokeAsync(async () =>
                {
                    PlaybackStatus = _localizationService["Playback_StatusAbsoluteCoordinatesUnsupported"];
                    _statusUpdateTimer?.Stop();

                    if (_dialogService is not null)
                    {
                        await _dialogService.ShowMessageAsync(
                            _localizationService["Playback_AbsoluteCoordinatesUnsupportedTitle"],
                            _localizationService["Playback_AbsoluteCoordinatesUnsupportedMessage"]).ConfigureAwait(true);
                    }
                }).ConfigureAwait(false);
            }
            else if (ex is InputInjectionPermissionRequiredException)
            {
                await UiDispatcher.InvokeAsync(async () =>
                {
                    PlaybackStatus = _localizationService["Playback_StatusPermissionRequired"];
                    _statusUpdateTimer?.Stop();

                    if (_dialogService is not null)
                    {
                        await _dialogService.ShowMessageAsync(
                            _localizationService["Playback_PermissionRequiredTitle"],
                            _localizationService["Playback_PermissionRequiredMessage"]).ConfigureAwait(true);
                    }
                }).ConfigureAwait(false);
            }
            else
            {
                await UiDispatcher.InvokeAsync(() =>
                {
                    PlaybackStatus = string.Format(_localizationService.CurrentCulture, _localizationService["Playback_StatusError"], ex.Message);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
            }
        }
        finally
        {
            await UiDispatcher.InvokeAsync(() =>
            {
                if (!_disposed && completedNormally)
                {
                    PlaybackStatus = _localizationService["Playback_StatusComplete"];
                }

                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }
    }

    public void StopPlayback()
    {
        lock (_playbackGate)
        {
            var session = _playbackSession;
            if (session is null && !IsPlaying) { return; }

            Volatile.Write(ref _stopRequested, 1);
            session?.RequestStop(_player);
            _isWaitingBetweenSequenceCycles = false;
            _statusUpdateTimer?.Stop();
            IsPaused = false;
            PlaybackStatus = _localizationService["Playback_StatusStopped"];
            if (session is null) { IsPlaying = false; }
        }
    }

    public void TogglePause()
    {
        if (!IsPlaying || _isWaitingBetweenSequenceCycles || StopRequested)
        {
            return;
        }

        if (_player.IsPaused)
        {
            _player.ResumePlayback();
            IsPaused = false;
            ApplyPlaybackStatus();
        }
        else
        {
            _player.Pause();
            IsPaused = true;
            PlaybackStatus = _localizationService["Playback_StatusPaused"];
        }
    }

    /// <summary>
    /// Toggle playback state (for hotkey handling)
    /// </summary>
    public void TogglePlayback()
    {
        if (IsPlaying || _playbackSession is not null)
        {
            StopPlayback();
        }
        else if (CanPlayMacro && CanPlayMacroExternal)
        {
            _ = PlayMacroAsync();
        }
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

        lock (_playbackGate)
        {
            _disposed = true;
            _playbackSession?.RequestStop(_player);
        }
        _statusUpdateTimer?.Stop();
        _statusUpdateTimer?.Tick -= OnStatusUpdateTimerTick;
        _statusUpdateTimer = null;

        _localizationService.CultureChanged -= OnCultureChanged;
        _loadedMacroSession.SelectedMacroChanged -= OnLoadedMacroSelectionChanged;
        _loadedMacroSession.SelectedMacroUpdated -= OnLoadedMacroUpdated;
        _loadedMacroSession.PlaybackModeChanged -= OnLoadedMacroPlaybackModeChanged;
    }

    private PlaybackOptions BuildSingleMacroPlaybackOptions()
    {
        return new PlaybackOptions
        {
            SpeedMultiplier = PlaybackOptions.NormalizeSpeedMultiplier(PlaybackSpeed),
            Loop = IsLooping,
            RepeatCount = LoopCount,
            RepeatDelayMs = LoopDelayMs ?? 0,
            UseRandomRepeatDelay = UseRandomLoopDelay,
            RepeatDelayMinMs = LoopDelayMinMs ?? 0,
            RepeatDelayMaxMs = LoopDelayMaxMs ?? 0,
            MotionMode = MotionPlaybackMode,
            PrecisionMotionEventsPerSecond = PrecisionMotionEventsPerSecond ?? PlaybackOptions.DefaultPrecisionMotionEventsPerSecond,
            StrictSpeedMotionEventsPerSecond = StrictSpeedMotionEventsPerSecond ?? PlaybackOptions.DefaultStrictSpeedMotionEventsPerSecond,
            MaximumMotionErrorPixels = MaximumMotionErrorPixels,
        };
    }

    private PlaybackOptions BuildSequenceMacroPlaybackOptions(LoadedMacroListItem item)
    {
        var repeatCount = Math.Max(1, item.SequenceRepeatCount);

        return new PlaybackOptions
        {
            SpeedMultiplier = PlaybackOptions.NormalizeSpeedMultiplier(PlaybackSpeed),
            Loop = repeatCount > 1,
            RepeatCount = repeatCount,
            RepeatDelayMs = 0,
            UseRandomRepeatDelay = false,
            RepeatDelayMinMs = 0,
            RepeatDelayMaxMs = 0,
            MotionMode = MotionPlaybackMode,
            PrecisionMotionEventsPerSecond = PrecisionMotionEventsPerSecond ?? PlaybackOptions.DefaultPrecisionMotionEventsPerSecond,
            StrictSpeedMotionEventsPerSecond = StrictSpeedMotionEventsPerSecond ?? PlaybackOptions.DefaultStrictSpeedMotionEventsPerSecond,
            MaximumMotionErrorPixels = MaximumMotionErrorPixels,
        };
    }

    private async Task WaitForCountdownAsync(CancellationToken cancellationToken)
    {
        var countdown = CountdownSeconds ?? 0;
        if (countdown <= 0)
        {
            return;
        }

        for (var i = countdown; i > 0; i--)
        {
            await UiDispatcher.InvokeAsync(() =>
            {
                PlaybackStatus = string.Format(_localizationService.CurrentCulture, _localizationService["Playback_StatusStartingIn"], i);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            if (StopRequested)
            {
                return;
            }
        }
    }

    private async Task PlaySingleMacroModeAsync(
        PlaybackSession session,
        MacroSequence macro,
        LoadedMacroPlaybackMode playbackMode)
    {
        await UpdatePlaybackStatusAsync().ConfigureAwait(false);
        await session.PlayAsync(_player, macro, BuildSingleMacroPlaybackOptions()).ConfigureAwait(false);
        if (StopRequested)
        {
            return;
        }

        if (playbackMode is LoadedMacroPlaybackMode.AdvanceSelection)
        {
            _ = await UiDispatcher.InvokeAsync(_loadedMacroSession.SelectNext).ConfigureAwait(false);
        }
    }

    private async Task PlaySequentialCycleAsync(
        PlaybackSession session,
        IReadOnlyList<LoadedMacroListItem> sequenceSnapshot,
        CancellationToken cancellationToken)
    {
        if (sequenceSnapshot.Count is 0)
        {
            return;
        }

        await UiDispatcher.InvokeAsync(() =>
        {
            _isSequencePlayback = true;
            _sequenceMacroCount = sequenceSnapshot.Count;
            _sequenceTotalCycles = IsLooping ? LoopCount : 1;
            return Task.CompletedTask;
        }).ConfigureAwait(false);

        var startItemSessionId = sequenceSnapshot[0].SessionId;
        var infiniteCycles = IsLooping && LoopCount is 0;
        var completedCycles = 0;

        try
        {
            while ((infiniteCycles || completedCycles < _sequenceTotalCycles) && !cancellationToken.IsCancellationRequested)
            {
                for (var index = 0; index < sequenceSnapshot.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var item = sequenceSnapshot[index];
                    await UiDispatcher.InvokeAsync(() =>
                    {
                        _sequenceCycle = completedCycles + 1;
                        _sequenceMacroIndex = index + 1;
                        _sequenceMacroName = item.Name;
                        _sequenceMacroRepeatCount = item.SequenceRepeatCount;
                        SelectLiveMacroBySessionId(item.SessionId);
                        ApplyPlaybackStatus();
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);

                    await session.PlayAsync(_player, item.Macro, BuildSequenceMacroPlaybackOptions(item)).ConfigureAwait(false);
                    if (StopRequested)
                    {
                        return;
                    }
                }

                completedCycles++;
                var hasNextCycle = infiniteCycles || completedCycles < _sequenceTotalCycles;
                if (!hasNextCycle)
                {
                    break;
                }

                await UiDispatcher.InvokeAsync(() =>
                {
                    SelectLiveMacroBySessionId(startItemSessionId);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
                var cycleDelay = ResolveSequenceCycleDelayMs();
                if (cycleDelay > 0)
                {
                    await UiDispatcher.InvokeAsync(() =>
                    {
                        _isWaitingBetweenSequenceCycles = true;
                        ApplyPlaybackStatus();
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                    await Task.Delay(cycleDelay, cancellationToken).ConfigureAwait(false);
                    await UiDispatcher.InvokeAsync(() =>
                    {
                        _isWaitingBetweenSequenceCycles = false;
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            await UiDispatcher.InvokeAsync(() =>
            {
                SelectLiveMacroBySessionId(startItemSessionId);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }
    }

    private void SelectLiveMacroBySessionId(Guid sessionId)
    {
        foreach (var item in _loadedMacroSession.LoadedMacros)
        {
            if (item.SessionId == sessionId)
            {
                _loadedMacroSession.SelectedMacroItem = item;
                return;
            }
        }
    }

    private int ResolveSequenceCycleDelayMs()
    {
        if (!UseRandomLoopDelay)
        {
            return Math.Max(0, LoopDelayMs ?? 0);
        }

        var min = Math.Max(0, LoopDelayMinMs ?? 0);
        var max = Math.Max(0, LoopDelayMaxMs ?? 0);
        if (max < min)
        {
            max = min;
        }

        if (min == max)
        {
            return min;
        }

        return min == max ? min : _randomInclusive(min, max);
    }

    private void ResetSequenceState()
    {
        _isSequencePlayback = false;
        _isWaitingBetweenSequenceCycles = false;
        _sequenceMacroIndex = 0;
        _sequenceMacroCount = 0;
        _sequenceCycle = 0;
        _sequenceTotalCycles = 0;
        _sequenceMacroName = string.Empty;
        _sequenceMacroRepeatCount = 1;
    }

    private void OnLoadedMacroSelectionChanged(object? sender, EventArgs e)
    {
        PostToUiThread(NotifyPlaybackAvailabilityChanged);
    }

    private void OnLoadedMacroUpdated(object? sender, EventArgs e)
    {
        PostToUiThread(NotifyPlaybackAvailabilityChanged);
    }

    private void OnLoadedMacroPlaybackModeChanged(object? sender, EventArgs e)
    {
        PostToUiThread(NotifyPlaybackAvailabilityChanged);
    }

    private void NotifyPlaybackAvailabilityChanged()
    {
        OnPropertyChanged(nameof(HasMacro));
        OnPropertyChanged(nameof(CanPlayMacro));
    }

    private Task UpdatePlaybackStatusAsync()
    {
        return UiDispatcher.InvokeAsync(() =>
        {
            ApplyPlaybackStatus();
            return Task.CompletedTask;
        });
    }

    private static int NormalizeDelayInput(int? value)
    {
        return PlaybackOptions.NormalizeDelayMs(value ?? 0);
    }

    private void UpdateLoopDelayRange(int minMs, int maxMs)
    {
        var (normalizedMin, normalizedMax) = PlaybackOptions.NormalizeDelayRange(minMs, maxMs);
        _loopDelayMinMs = normalizedMin;
        _loopDelayMaxMs = normalizedMax;
        _settingsDraft.LoopDelayMinMs = normalizedMin;
        _settingsDraft.LoopDelayMaxMs = normalizedMax;
    }

    private string GetLoopDelayWaitText()
    {
        if (!UseRandomLoopDelay)
        {
            return $"{(LoopDelayMs ?? 0).ToString(CultureInfo.InvariantCulture)} ms";
        }

        var min = LoopDelayMinMs ?? 0;
        var max = LoopDelayMaxMs ?? 0;
        return min == max ? $"{min.ToString(CultureInfo.InvariantCulture)} ms" : $"{min.ToString(CultureInfo.InvariantCulture)}-{max.ToString(CultureInfo.InvariantCulture)} ms";
    }

    private void PersistLoopSettingChange(Action rollback, params string[] propertyNames)
    {
        var changeVersion = Interlocked.Increment(ref _settingsChangeVersion);
        _ = PersistLoopSettingChangeAsync(changeVersion, rollback, propertyNames);
    }

    private async Task PersistLoopSettingChangeAsync(int changeVersion, Action rollback, string[] propertyNames)
    {
        if (!IsFastLoopRisky())
        {
            _fastLoopWarningAcknowledged = false;
        }

        if (!await ConfirmFastLoopPlaybackAsync(forPlayback: false).ConfigureAwait(false))
        {
            await RunOnUiThreadAsync(() =>
            {
                if (Volatile.Read(ref _settingsChangeVersion) == changeVersion)
                {
                    rollback();
                    foreach (var propertyName in propertyNames)
                    {
                        OnPropertyChanged(propertyName);
                    }
                }
            }).ConfigureAwait(false);
            return;
        }

        _ = TryPersistSettingChange(propertyNames);
    }

    private async Task<bool> ConfirmFastLoopPlaybackAsync(bool forPlayback)
    {
        if (!IsFastLoopRisky() || _fastLoopWarningAcknowledged || _settingsDraft.SuppressFastLoopWarning)
        {
            return true;
        }

        var dialogService = _dialogService;
        if (dialogService is null)
        {
            return false;
        }

        var warningTask = _fastLoopWarningTask;
        if (warningTask is not null)
        {
            await warningTask.ConfigureAwait(false);
            return !IsFastLoopRisky() || _fastLoopWarningAcknowledged || _settingsDraft.SuppressFastLoopWarning;
        }

        warningTask = ShowFastLoopWarningAsync(dialogService, forPlayback);
        _fastLoopWarningTask = warningTask;
        try
        {
            await warningTask.ConfigureAwait(false);
        }
        finally
        {
            _fastLoopWarningTask = null;
        }

        return !IsFastLoopRisky() || _fastLoopWarningAcknowledged || _settingsDraft.SuppressFastLoopWarning;
    }

    private async Task ShowFastLoopWarningAsync(IDialogService dialogService, bool forPlayback)
    {
        var result = await dialogService.ShowFastLoopWarningAsync(
            _localizationService["Playback_FastLoopWarningTitle"],
            _localizationService["Playback_FastLoopWarningMessage"],
            _localizationService[forPlayback ? "Playback_FastLoopWarningPlay" : "Playback_FastLoopWarningContinue"],
            _localizationService[forPlayback ? "Playback_FastLoopWarningAbort" : "Playback_FastLoopWarningCancel"],
            _localizationService["Playback_FastLoopWarningSuppress"]).ConfigureAwait(false);
        if (_disposed || !result.ContinuePlayback)
        {
            return;
        }

        _fastLoopWarningAcknowledged = true;
        if (!result.SuppressFutureWarnings)
        {
            return;
        }

        _settingsDraft.SuppressFastLoopWarning = true;
        _ = TryPersistSettingChange(nameof(AppSettings.SuppressFastLoopWarning));
    }

    private bool IsFastLoopRisky()
    {
        if (!IsLooping || LoopCount is not 0 and <= 1)
        {
            return false;
        }

        return UseRandomLoopDelay
            ? (LoopDelayMinMs ?? 0) < FastLoopWarningThresholdMs
            : (LoopDelayMs ?? 0) < FastLoopWarningThresholdMs;
    }

    private bool TryPersistSettingChange(params string[] propertyNames)
    {
        var request = new SettingsChangeRequest(_lastSubmittedSettings, AppSettingsSnapshot.Copy(_settingsDraft));
        _lastSubmittedSettings = AppSettingsSnapshot.Copy(_settingsDraft);
        _ = PersistSettingChangeAsync(request, propertyNames);
        return true;
    }

    private async Task PersistSettingChangeAsync(SettingsChangeRequest request, string[] propertyNames)
    {
        try
        {
            await _settingsChanges.CommitAsync(request, SettingsSaveMode.AfterIdle, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            await RunOnUiThreadAsync(() =>
            {
                RefreshSettingsPresentation();
                foreach (var propertyName in propertyNames) { OnPropertyChanged(propertyName); }
            }).ConfigureAwait(false);
            Log.LogError(error, "Failed to persist playback settings");
        }
    }
}
