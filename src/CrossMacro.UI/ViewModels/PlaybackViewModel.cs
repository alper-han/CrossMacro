
namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Playback tab - handles macro playback functionality
/// </summary>
public partial class PlaybackViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;

    private readonly IMacroPlayer _player;
    private readonly ISettingsService _settingsService;
    private readonly ILoadedMacroSession _loadedMacroSession;
    private readonly ILocalizationService _localizationService;
    private readonly IDialogService? _dialogService;
    private readonly Func<int, int, int> _randomInclusive;
    private readonly Func<Func<Task>, Task> _executeOnUiThread;

    private double _playbackSpeed = 1.0;
    private int? _loopDelayMs = 0;
    private int? _loopDelayMinMs = 0;
    private int? _loopDelayMaxMs = 0;
    private string _playbackStatus;
    private int _stopRequested;
    private int _settingsChangeVersion;

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

    private MacroSequence? _currentMacro;
    private CancellationTokenSource? _playbackCts;
    private DispatcherTimer? _statusUpdateTimer;
    private SynchronizationContext? _uiSynchronizationContext;

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
        IDialogService? dialogService = null)
        : this(player, settingsService, loadedMacroSession, localizationService, dialogService, RandomNumberGeneratorUtility.GetInt32Inclusive) { /* Empty */ }

    internal PlaybackViewModel(
        IMacroPlayer player,
        ISettingsService settingsService,
        ILoadedMacroSession loadedMacroSession,
        ILocalizationService? localizationService,
        IDialogService? dialogService,
        Func<int, int, int> randomInclusive,
        Func<Func<Task>, Task>? executeOnUiThread = null)
    {
        _player = player;
        _settingsService = settingsService;
        _loadedMacroSession = loadedMacroSession;
        _localizationService = localizationService ?? new LocalizationService();
        _dialogService = dialogService;
        _randomInclusive = randomInclusive ?? throw new ArgumentNullException(nameof(randomInclusive));
        _executeOnUiThread = executeOnUiThread ?? ExecuteOnUiThreadAsync;
        _playbackStatus = _localizationService["Playback_StatusReady"];

        // Initialize playback settings from saved settings
        _playbackSpeed = _settingsService.Current.PlaybackSpeed;
        _isLooping = _settingsService.Current.IsLooping;
        _loopCount = _settingsService.Current.LoopCount;
        _loopDelayMs = _settingsService.Current.LoopDelayMs;
        _useRandomLoopDelay = _settingsService.Current.UseRandomLoopDelay;
        _loopDelayMinMs = _settingsService.Current.LoopDelayMinMs;
        _loopDelayMaxMs = _settingsService.Current.LoopDelayMaxMs;
        _countdownSeconds = _settingsService.Current.CountdownSeconds;
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

        // Direct field writes: refreshing from settings must not re-persist via setter hooks.
#pragma warning disable MVVMTK0034
        _playbackSpeed = _settingsService.Current.PlaybackSpeed;
        _isLooping = _settingsService.Current.IsLooping;
        _loopCount = _settingsService.Current.LoopCount;
        _loopDelayMs = _settingsService.Current.LoopDelayMs;
        _useRandomLoopDelay = _settingsService.Current.UseRandomLoopDelay;
        _loopDelayMinMs = _settingsService.Current.LoopDelayMinMs;
        _loopDelayMaxMs = _settingsService.Current.LoopDelayMaxMs;
        _countdownSeconds = _settingsService.Current.CountdownSeconds;
#pragma warning restore MVVMTK0034

        OnPropertyChanged(nameof(PlaybackSpeed));
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
                var previousValue = _playbackSpeed;
                _playbackSpeed = normalized;
                _settingsService.Current.PlaybackSpeed = normalized;
                OnPropertyChanged();
                _ = TryPersistSettingChange(
                    () =>
                    {
                        _playbackSpeed = previousValue;
                        _settingsService.Current.PlaybackSpeed = previousValue;
                    },
                    nameof(PlaybackSpeed));
            }
        }
    }

    partial void OnIsLoopingChanged(bool oldValue, bool newValue)
    {
        _settingsService.Current.IsLooping = newValue;
        _ = TryPersistSettingChange(
            () =>
            {
                _isLooping = oldValue;
                _settingsService.Current.IsLooping = oldValue;
            },
            nameof(IsLooping),
            nameof(ShowFixedLoopDelayInput),
            nameof(ShowRandomLoopDelayInputs));
    }

    partial void OnLoopCountChanged(int oldValue, int newValue)
    {
        _settingsService.Current.LoopCount = newValue;
        _ = TryPersistSettingChange(
            () =>
            {
                _loopCount = oldValue;
                _settingsService.Current.LoopCount = oldValue;
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
                _settingsService.Current.LoopDelayMs = normalized;
                OnPropertyChanged();
                _ = TryPersistSettingChange(
                    () =>
                    {
                        _loopDelayMs = previousValue;
                        _settingsService.Current.LoopDelayMs = previousValue;
                    },
                    nameof(LoopDelayMs));
            }
        }
    }

    partial void OnUseRandomLoopDelayChanged(bool oldValue, bool newValue)
    {
        var previousMin = _loopDelayMinMs ?? 0;
        var previousMax = _loopDelayMaxMs ?? 0;

        if (newValue && previousMin is 0 && previousMax is 0)
        {
            var seededDelay = NormalizeDelayInput(LoopDelayMs);
            UpdateLoopDelayRange(seededDelay, seededDelay);
        }

        _settingsService.Current.UseRandomLoopDelay = newValue;

        _ = TryPersistSettingChange(
            () =>
            {
                _useRandomLoopDelay = oldValue;
                UpdateLoopDelayRange(previousMin, previousMax);
                _settingsService.Current.UseRandomLoopDelay = oldValue;
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
            _settingsService.Current.LoopDelayMinMs = normalizedMin;
            _settingsService.Current.LoopDelayMaxMs = normalizedMax;
            OnPropertyChanged();
            if (previousMax != normalizedMax)
            {
                OnPropertyChanged(nameof(LoopDelayMaxMs));
            }

            _ = TryPersistSettingChange(
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
            _settingsService.Current.LoopDelayMinMs = normalizedMin;
            _settingsService.Current.LoopDelayMaxMs = normalizedMax;
            OnPropertyChanged();
            if (previousMin != normalizedMin)
            {
                OnPropertyChanged(nameof(LoopDelayMinMs));
            }

            _ = TryPersistSettingChange(
                () => UpdateLoopDelayRange(previousMin, previousMax),
                nameof(LoopDelayMinMs),
                nameof(LoopDelayMaxMs));
        }
    }

    public bool ShowFixedLoopDelayInput => IsLooping && !UseRandomLoopDelay;

    public bool ShowRandomLoopDelayInputs => IsLooping && UseRandomLoopDelay;

    partial void OnCountdownSecondsChanged(int? oldValue, int? newValue)
    {
        _settingsService.Current.CountdownSeconds = newValue ?? 0;
        _ = TryPersistSettingChange(
            () =>
            {
                _countdownSeconds = oldValue;
                _settingsService.Current.CountdownSeconds = oldValue ?? 0;
            },
            nameof(CountdownSeconds));
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
        if (IsPlaying || !CanPlayMacroExternal)
        {
            return;
        }

        var currentSynchronizationContext = SynchronizationContext.Current;
        var contextTypeName = currentSynchronizationContext?.GetType().FullName;
        _uiSynchronizationContext = currentSynchronizationContext is not null
            && (contextTypeName is null || !contextTypeName.StartsWith("Avalonia.", StringComparison.Ordinal))
            ? currentSynchronizationContext
            : null;

        var executionPlan = PlaybackExecutionPlanner.CreatePlan(_loadedMacroSession, _currentMacro);
        if (!string.IsNullOrEmpty(executionPlan.ValidationError))
        {
            await _executeOnUiThread(() =>
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

        var playbackMode = executionPlan.Mode;
        var activeMacro = executionPlan.ActiveMacro!;
        var sequenceSnapshot = executionPlan.SequenceSnapshot;

        _playbackCts?.Dispose();
        var playbackCts = new CancellationTokenSource();
        _playbackCts = playbackCts;
        Volatile.Write(ref _stopRequested, 0);
        var completedNormally = false;

        try
        {
            await _executeOnUiThread(() =>
            {
                ResetSequenceState();
                IsPlaying = true;
                IsPaused = false;
                return Task.CompletedTask;
            }).ConfigureAwait(false);

            await WaitForCountdownAsync(playbackCts.Token).ConfigureAwait(false);
            if (StopRequested)
            {
                return;
            }

            await _executeOnUiThread(() =>
            {
                _statusUpdateTimer?.Start();
                return Task.CompletedTask;
            }).ConfigureAwait(false);

            if (executionPlan.UsesSequence)
            {
                await PlaySequentialCycleAsync(sequenceSnapshot, playbackCts.Token).ConfigureAwait(false);
            }
            else
            {
                await PlaySingleMacroModeAsync(activeMacro, playbackMode, playbackCts.Token).ConfigureAwait(false);
            }

            completedNormally = !StopRequested;
        }
        catch (OperationCanceledException) when (StopRequested) { /* Empty */ }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (ex is AbsolutePlaybackUnsupportedException)
            {
                await _executeOnUiThread(async () =>
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
                await _executeOnUiThread(async () =>
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
                await _executeOnUiThread(() =>
                {
                    PlaybackStatus = string.Format(_localizationService.CurrentCulture, _localizationService["Playback_StatusError"], ex.Message);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
            }
        }
        finally
        {
            await _executeOnUiThread(() =>
            {
                if (completedNormally)
                {
                    PlaybackStatus = _localizationService["Playback_StatusComplete"];
                }

                _statusUpdateTimer?.Stop();
                _playbackCts?.Dispose();
                _playbackCts = null;
                ResetSequenceState();
                IsPlaying = false;
                IsPaused = false;
                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }
    }

    public void StopPlayback()
    {
        if (!IsPlaying)
        {
            return;
        }

        Volatile.Write(ref _stopRequested, 1);
        _isWaitingBetweenSequenceCycles = false;
        _statusUpdateTimer?.Stop();
        _playbackCts?.Cancel();
        IsPaused = false;
        _player.StopPlayback();
        PlaybackStatus = _localizationService["Playback_StatusStopped"];

        if (_playbackCts is null)
        {
            IsPlaying = false;
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
        if (IsPlaying)
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

        _disposed = true;
        _statusUpdateTimer?.Stop();
        _statusUpdateTimer?.Tick -= OnStatusUpdateTimerTick;
        _statusUpdateTimer = null;

        _localizationService.CultureChanged -= OnCultureChanged;
        _loadedMacroSession.SelectedMacroChanged -= OnLoadedMacroSelectionChanged;
        _loadedMacroSession.SelectedMacroUpdated -= OnLoadedMacroUpdated;
        _loadedMacroSession.PlaybackModeChanged -= OnLoadedMacroPlaybackModeChanged;
        _playbackCts?.Dispose();
        _playbackCts = null;
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
            await _executeOnUiThread(() =>
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
        MacroSequence macro,
        LoadedMacroPlaybackMode playbackMode,
        CancellationToken cancellationToken)
    {
        await UpdatePlaybackStatusAsync().ConfigureAwait(false);
        await _player.PlayAsync(macro, BuildSingleMacroPlaybackOptions(), cancellationToken).ConfigureAwait(false);
        if (StopRequested)
        {
            return;
        }

        if (playbackMode is LoadedMacroPlaybackMode.AdvanceSelection)
        {
            await _executeOnUiThread(() =>
            {
                return Task.FromResult(_loadedMacroSession.SelectNext());
            }).ConfigureAwait(false);
        }
    }

    private async Task PlaySequentialCycleAsync(
        IReadOnlyList<LoadedMacroListItem> sequenceSnapshot,
        CancellationToken cancellationToken)
    {
        if (sequenceSnapshot.Count is 0)
        {
            return;
        }

        await _executeOnUiThread(() =>
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
                    await _executeOnUiThread(() =>
                    {
                        _sequenceCycle = completedCycles + 1;
                        _sequenceMacroIndex = index + 1;
                        _sequenceMacroName = item.Name;
                        _sequenceMacroRepeatCount = item.SequenceRepeatCount;
                        SelectLiveMacroBySessionId(item.SessionId);
                        ApplyPlaybackStatus();
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);

                    await _player.PlayAsync(item.Macro, BuildSequenceMacroPlaybackOptions(item), cancellationToken).ConfigureAwait(false);
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

                await _executeOnUiThread(() =>
                {
                    SelectLiveMacroBySessionId(startItemSessionId);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
                var cycleDelay = ResolveSequenceCycleDelayMs();
                if (cycleDelay > 0)
                {
                    await _executeOnUiThread(() =>
                    {
                        _isWaitingBetweenSequenceCycles = true;
                        ApplyPlaybackStatus();
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                    await Task.Delay(cycleDelay, cancellationToken).ConfigureAwait(false);
                    await _executeOnUiThread(() =>
                    {
                        _isWaitingBetweenSequenceCycles = false;
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            await _executeOnUiThread(() =>
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
        return _executeOnUiThread(() =>
        {
            ApplyPlaybackStatus();
            return Task.CompletedTask;
        });
    }

    private async Task ExecuteOnUiThreadAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_uiSynchronizationContext is not null)
        {
            if (ReferenceEquals(SynchronizationContext.Current, _uiSynchronizationContext))
            {
                await action().ConfigureAwait(true);
            }
            else
            {
                await ExecuteOnSynchronizationContextAsync(_uiSynchronizationContext, action).ConfigureAwait(false);
            }

            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            await action().ConfigureAwait(true);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action).ConfigureAwait(false);
    }

    private static Task ExecuteOnSynchronizationContextAsync(SynchronizationContext context, Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Post(
            state =>
            {
                var execution = ExecuteAsync((Func<Task>)state!);
                execution.GetAwaiter().OnCompleted(() =>
                {
                    if (execution.IsCompletedSuccessfully)
                    {
                        completion.SetResult();
                    }
                    else if (execution.IsCanceled)
                    {
                        completion.SetCanceled(new CancellationToken(canceled: true));
                    }
                    else if (execution.Exception is { } exception)
                    {
                        completion.SetException(exception.InnerExceptions);
                    }
                });
            },
            action);
        return completion.Task;

        static async Task ExecuteAsync(Func<Task> callback)
        {
            await callback().ConfigureAwait(true);
        }
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
        _settingsService.Current.LoopDelayMinMs = normalizedMin;
        _settingsService.Current.LoopDelayMaxMs = normalizedMax;
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

    private bool TryPersistSettingChange(Action rollback, params string[] propertyNames)
    {
        var changeVersion = Interlocked.Increment(ref _settingsChangeVersion);
        _ = TryPersistSettingChangeAsync(changeVersion, rollback, propertyNames);
        return true;
    }

    private async Task TryPersistSettingChangeAsync(int changeVersion, Action rollback, string[] propertyNames)
    {
        try
        {
            await _settingsService.SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (Volatile.Read(ref _settingsChangeVersion) == changeVersion)
            {
                await RunOnUiThreadAsync(() =>
                {
                    rollback();
                    foreach (var propertyName in propertyNames)
                    {
                        OnPropertyChanged(propertyName);
                    }
                }).ConfigureAwait(false);
            }

            Log.LogError(ex, "[PlaybackViewModel] Failed to persist playback settings");
        }
    }
}
