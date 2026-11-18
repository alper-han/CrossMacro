using CrossMacro.UI.Services.Settings;

namespace CrossMacro.UI.ViewModels.Playback;

/// <summary>
/// ViewModel for the Playback tab - handles macro playback functionality
/// </summary>
public partial class PlaybackViewModel : PlaybackSettingsViewModel, IDisposable
{
    private bool _disposed;

    private readonly IMacroPlayer _player;
    private readonly IDialogService _dialogService;
    private readonly ILoadedMacroSession _loadedMacroSession;
    private readonly Func<int, int, int> _randomInclusive;

    private string _playbackStatus;
    private int _stopRequested;

    /// <summary>
    /// Used by MainWindowViewModel to control if playback can start (considering recording state)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPlayMacro))]
    private bool _canPlayMacroExternal = true;
    private readonly SequencePlaybackProgress _sequence = new();
    private readonly PlaybackStatusFormatter _statusFormatter;
    private readonly PlaybackSequenceRunner _sequenceRunner;

    private readonly Lock _playbackGate = new();
    private PlaybackSession? _playbackSession;
    private volatile bool _statusUpdatesActive;
    private int _statusRefreshQueued;

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
        ILocalizationService localizationService,
        IDialogService dialogService,
        SettingsChangeCoordinator settingsChanges,
        IUiDispatcher? uiDispatcher = null,
        Func<int, int, int>? randomInclusive = null)
        : base(settingsService, localizationService, dialogService, settingsChanges, uiDispatcher)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _loadedMacroSession = loadedMacroSession ?? throw new ArgumentNullException(nameof(loadedMacroSession));
        _dialogService = dialogService;
        _randomInclusive = randomInclusive ?? RandomNumberGeneratorUtility.GetInt32Inclusive;
        _playbackStatus = LocalizationService["Playback_StatusReady"];
        _statusFormatter = new PlaybackStatusFormatter(LocalizationService);
        _sequenceRunner = new PlaybackSequenceRunner(
            UiDispatcher,
            _player,
            _loadedMacroSession,
            _sequence,
            () => StopRequested,
            ApplyPlaybackStatus,
            BuildSequenceMacroPlaybackOptions,
            ResolveSequenceCycleDelayMs);

        _loadedMacroSession.SelectedMacroChanged += OnLoadedMacroSelectionChanged;
        _loadedMacroSession.SelectedMacroUpdated += OnLoadedMacroUpdated;
        _loadedMacroSession.PlaybackModeChanged += OnLoadedMacroPlaybackModeChanged;
        LocalizationService.CultureChanged += OnCultureChanged;

        _player.StateChanged += OnPlayerStateChanged;
    }

    public void RefreshProfileSettings()
    {
        if (IsPlaying)
        {
            return;
        }

        RefreshSettingsPresentation();
    }

    protected override bool IsDisposed => _disposed;

    private protected override void OnSettingsPresentationRefreshed() => OnPropertyChanged(nameof(CanPlayMacro));

    private void OnPlayerStateChanged(object? sender, EventArgs e)
    {
        if (!_statusUpdatesActive || Interlocked.Exchange(ref _statusRefreshQueued, 1) is not 0)
        {
            return;
        }

        PostToUiThread(RefreshStatusFromPlayer);
    }

    private void RefreshStatusFromPlayer()
    {
        Volatile.Write(ref _statusRefreshQueued, 0);
        if (_statusUpdatesActive && IsPlaying && !IsPaused && !StopRequested)
        {
            ApplyPlaybackStatus();
        }
    }

    private void ApplyPlaybackStatus()
    {
        var isWaiting = _sequence.IsActive ? _sequence.IsWaitingBetweenCycles : _player.IsWaitingBetweenLoops;
        PlaybackStatus = _statusFormatter.Format(_player, _sequence, isWaiting ? GetLoopDelayWaitText() : string.Empty);
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
                PlaybackStatus = LocalizationService["Playback_StatusPaused"];
                return;
            }

            if (StopRequested)
            {
                PlaybackStatus = LocalizationService["Playback_StatusStopped"];
                return;
            }

            PlaybackStatus = LocalizationService["Playback_StatusReady"];
        });
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
        PlaybackExecutionPlanner.GetPreviewMacro(_loadedMacroSession));

    public bool CanPlayMacro => HasMacro && !IsPlaying && CanPlayMacroExternal;

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
                            _statusUpdatesActive = false;
                            _sequence.Reset();
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
        var executionPlan = PlaybackExecutionPlanner.CreatePlan(_loadedMacroSession);
        if (executionPlan.MacroWithoutEventsName is { } macroWithoutEventsName)
        {
            await ReportMacroWithoutEventsAsync(macroWithoutEventsName).ConfigureAwait(false);
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
            await MarkPlaybackStartedAsync(session).ConfigureAwait(false);

            await WaitForCountdownAsync(session.Token).ConfigureAwait(false);
            if (StopRequested)
            {
                return;
            }

            await UiDispatcher.InvokeAsync(() =>
            {
                _statusUpdatesActive = true;
                return Task.CompletedTask;
            }).ConfigureAwait(false);

            if (executionPlan.UsesSequence)
            {
                await _sequenceRunner.RunAsync(session, sequenceSnapshot, IsLooping, LoopCount, session.Token).ConfigureAwait(false);
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
            await ReportPlaybackFailureAsync(ex).ConfigureAwait(false);
        }
        finally
        {
            await UiDispatcher.InvokeAsync(() =>
            {
                if (!_disposed && completedNormally)
                {
                    PlaybackStatus = LocalizationService["Playback_StatusComplete"];
                }

                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }
    }

    private Task MarkPlaybackStartedAsync(PlaybackSession session)
    {
        return UiDispatcher.InvokeAsync(() =>
        {
            lock (_playbackGate)
            {
                session.Token.ThrowIfCancellationRequested();
                if (_disposed || !CanPlayMacroExternal) { session.RequestStop(_player); return Task.CompletedTask; }
                _sequence.Reset();
                IsPlaying = true;
                IsPaused = false;
            }
            return Task.CompletedTask;
        });
    }

    private Task ReportMacroWithoutEventsAsync(string macroName)
    {
        return UiDispatcher.InvokeAsync(() =>
        {
            PlaybackStatus = string.Format(
                LocalizationService.CurrentCulture,
                LocalizationService["Playback_ErrorMacroHasNoEvents"],
                macroName);
            return Task.CompletedTask;
        });
    }

    private async Task ReportPlaybackFailureAsync(Exception ex)
    {
        if (ex is AbsolutePlaybackUnsupportedException)
        {
            await UiDispatcher.InvokeAsync(async () =>
            {
                PlaybackStatus = LocalizationService["Playback_StatusAbsoluteCoordinatesUnsupported"];
                _statusUpdatesActive = false;

                await _dialogService.ShowMessageAsync(
                    LocalizationService["Playback_AbsoluteCoordinatesUnsupportedTitle"],
                    LocalizationService["Playback_AbsoluteCoordinatesUnsupportedMessage"]).ConfigureAwait(true);
            }).ConfigureAwait(false);
        }
        else if (ex is InputInjectionPermissionRequiredException)
        {
            await UiDispatcher.InvokeAsync(async () =>
            {
                PlaybackStatus = LocalizationService["Playback_StatusPermissionRequired"];
                _statusUpdatesActive = false;

                await _dialogService.ShowMessageAsync(
                    LocalizationService["Playback_PermissionRequiredTitle"],
                    LocalizationService["Playback_PermissionRequiredMessage"]).ConfigureAwait(true);
            }).ConfigureAwait(false);
        }
        else
        {
            await UiDispatcher.InvokeAsync(() =>
            {
                PlaybackStatus = string.Format(LocalizationService.CurrentCulture, LocalizationService["Playback_StatusError"], ex.Message);
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
            _sequence.IsWaitingBetweenCycles = false;
            _statusUpdatesActive = false;
            IsPaused = false;
            PlaybackStatus = LocalizationService["Playback_StatusStopped"];
            if (session is null) { IsPlaying = false; }
        }
    }

    public void TogglePause()
    {
        if (!IsPlaying || _sequence.IsWaitingBetweenCycles || StopRequested)
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
            PlaybackStatus = LocalizationService["Playback_StatusPaused"];
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
        _statusUpdatesActive = false;
        _player.StateChanged -= OnPlayerStateChanged;

        LocalizationService.CultureChanged -= OnCultureChanged;
        _loadedMacroSession.SelectedMacroChanged -= OnLoadedMacroSelectionChanged;
        _loadedMacroSession.SelectedMacroUpdated -= OnLoadedMacroUpdated;
        _loadedMacroSession.PlaybackModeChanged -= OnLoadedMacroPlaybackModeChanged;
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
                PlaybackStatus = string.Format(LocalizationService.CurrentCulture, LocalizationService["Playback_StatusStartingIn"], i);
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

        return min == max ? min : _randomInclusive(min, max);
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
}
