using CrossMacro.UI.Services.Settings;

namespace CrossMacro.UI.ViewModels.Playback;

/// <summary>
/// Persisted playback settings (speed, motion mode, looping, countdown): normalization, change notification,
/// persistence with rollback and the fast-loop confirmation.
/// </summary>
public abstract partial class PlaybackSettingsViewModel : ViewModelBase
{
    // External snapshots update presentation without re-running user-change effects.
    private bool _isRefreshingPresentation;

    private const int FastLoopWarningThresholdMs = 100;

    private readonly ISettingsService _settingsService;
    private readonly SettingsChangeCoordinator _settingsChanges;
    private readonly FastLoopWarningGuard _fastLoopGuard;
    private AppSettings _settingsDraft;
    private AppSettings _lastSubmittedSettings;

    private double _playbackSpeed = 1.0;
    private MotionPlaybackMode _motionPlaybackMode = MotionPlaybackMode.Precision;
    private int? _precisionMotionEventsPerSecond = PlaybackOptions.DefaultPrecisionMotionEventsPerSecond;
    private int? _strictSpeedMotionEventsPerSecond = PlaybackOptions.DefaultStrictSpeedMotionEventsPerSecond;
    private int? _loopDelayMs = 0;
    private int? _loopDelayMinMs = 0;
    private int? _loopDelayMaxMs = 0;
    private int _settingsChangeVersion;
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

    private protected PlaybackSettingsViewModel(
        ISettingsService settingsService,
        ILocalizationService localizationService,
        IDialogService dialogService,
        SettingsChangeCoordinator settingsChanges,
        IUiDispatcher? uiDispatcher)
        : base(uiDispatcher)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _settingsChanges = settingsChanges ?? throw new ArgumentNullException(nameof(settingsChanges));
        LocalizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        ArgumentNullException.ThrowIfNull(dialogService);
        _settingsDraft = settingsService.AccessCurrent(AppSettingsSnapshot.Copy);
        _lastSubmittedSettings = AppSettingsSnapshot.Copy(_settingsDraft);
        _fastLoopGuard = new FastLoopWarningGuard(
            UiDispatcher,
            dialogService,
            LocalizationService,
            IsFastLoopRisky,
            () => _settingsDraft.SuppressFastLoopWarning,
            SuppressFastLoopWarning,
            () => IsDisposed);

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
    }

    protected ILocalizationService LocalizationService { get; }

    protected abstract bool IsDisposed { get; }

    private protected void RefreshSettingsPresentation()
    {
        _settingsDraft = _settingsService.AccessCurrent(AppSettingsSnapshot.Copy);
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
        OnSettingsPresentationRefreshed();
    }


    /// <summary>Called after an external settings snapshot was applied to the presentation.</summary>
    private protected abstract void OnSettingsPresentationRefreshed();

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
                QueueSettingChange(nameof(PlaybackSpeed));
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
            QueueSettingChange(nameof(MotionPlaybackMode),
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
            QueueSettingChange(nameof(PrecisionMotionEventsPerSecond));
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
            QueueSettingChange(nameof(StrictSpeedMotionEventsPerSecond));
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
            QueueSettingChange(nameof(MaximumMotionErrorPixels));
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
        QueueSettingChange(nameof(CountdownSeconds));
    }

    private protected PlaybackOptions BuildSingleMacroPlaybackOptions()
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

    private protected PlaybackOptions BuildSequenceMacroPlaybackOptions(LoadedMacroListItem item)
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

    private protected Task<bool> ConfirmFastLoopPlaybackAsync(bool forPlayback) => _fastLoopGuard.ConfirmAsync(forPlayback);

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

    private protected string GetLoopDelayWaitText()
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
        _fastLoopGuard.ResetAcknowledgementWhenSafe();

        if (!await _fastLoopGuard.ConfirmAsync(forPlayback: false).ConfigureAwait(false))
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

        QueueSettingChange(propertyNames);
    }

    private void SuppressFastLoopWarning()
    {
        _settingsDraft.SuppressFastLoopWarning = true;
        QueueSettingChange(nameof(AppSettings.SuppressFastLoopWarning));
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

    private void QueueSettingChange(params string[] propertyNames)
    {
        _ = SettingsDraftPersistence.SubmitAsync(
            _settingsChanges, _settingsDraft, ref _lastSubmittedSettings,
            () => RunOnUiThreadAsync(() =>
            {
                RefreshSettingsPresentation();
                foreach (var propertyName in propertyNames) { OnPropertyChanged(propertyName); }
            }),
            "Failed to persist playback settings");
    }
}
