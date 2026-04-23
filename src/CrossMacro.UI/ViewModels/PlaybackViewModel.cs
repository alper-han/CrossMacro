using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Models;
using CrossMacro.UI.Services;
using Serilog;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Playback tab - handles macro playback functionality
/// </summary>
public class PlaybackViewModel : ViewModelBase, IDisposable
{
    private readonly IMacroPlayer _player;
    private readonly ISettingsService _settingsService;
    private readonly ILoadedMacroSession _loadedMacroSession;
    private readonly ILocalizationService _localizationService;
    private readonly Random _random = Random.Shared;

    private double _playbackSpeed = 1.0;
    private bool _isLooping;
    private int _loopCount = 1;
    private int? _loopDelayMs = 0;
    private bool _useRandomLoopDelay;
    private int? _loopDelayMinMs = 0;
    private int? _loopDelayMaxMs = 0;
    private int? _countdownSeconds = 0;
    private bool _isPlaying;
    private bool _isPaused;
    private string _playbackStatus;
    private bool _stopRequested;
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
        ILocalizationService? localizationService = null)
    {
        _player = player;
        _settingsService = settingsService;
        _loadedMacroSession = loadedMacroSession;
        _localizationService = localizationService ?? new LocalizationService();
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
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _statusUpdateTimer.Tick += OnStatusUpdateTimerTick;
    }

    private void OnStatusUpdateTimerTick(object? sender, EventArgs e)
    {
        if (IsPlaying && !IsPaused && !_stopRequested)
        {
            UpdatePlaybackStatus();
        }
    }

    private void UpdatePlaybackStatus()
    {
        if (_isSequencePlayback)
        {
            if (_isWaitingBetweenSequenceCycles)
            {
                PlaybackStatus = string.Format(_localizationService.CurrentCulture, _localizationService["Playback_StatusWaitingNextSequence"], GetLoopDelayWaitText());
                return;
            }

            var macroName = string.IsNullOrWhiteSpace(_sequenceMacroName) ? "macro" : _sequenceMacroName;
            var macroIndex = Math.Max(1, _sequenceMacroIndex);
            var macroCount = Math.Max(1, _sequenceMacroCount);
            var cycleText = _sequenceTotalCycles == 0
                ? $"{Math.Max(1, _sequenceCycle)} - Infinite"
                : $"{Math.Max(1, _sequenceCycle)}/{Math.Max(1, _sequenceTotalCycles)}";
            var repeatCount = Math.Max(1, _sequenceMacroRepeatCount);
            var repeatText = string.Empty;

            if (repeatCount > 1)
            {
                var currentRepeat = _player.TotalLoops == repeatCount
                    ? Math.Clamp(Math.Max(1, _player.CurrentLoop), 1, repeatCount)
                    : 1;
                repeatText = $" - Repeat {currentRepeat}/{repeatCount}";
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

        if (totalLoops == 0)
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
        if (IsPlaying && !IsPaused && !_stopRequested)
        {
            UpdatePlaybackStatus();
            return;
        }

        if (IsPaused)
        {
            PlaybackStatus = _localizationService["Playback_StatusPaused"];
            return;
        }

        if (_stopRequested)
        {
            PlaybackStatus = _localizationService["Playback_StatusStopped"];
            return;
        }

        PlaybackStatus = _localizationService["Playback_StatusReady"];
    }

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
                TryPersistSettingChange(
                    () =>
                    {
                        _playbackSpeed = previousValue;
                        _settingsService.Current.PlaybackSpeed = previousValue;
                    },
                    nameof(PlaybackSpeed));
            }
        }
    }

    public bool IsLooping
    {
        get => _isLooping;
        set
        {
            if (_isLooping != value)
            {
                var previousValue = _isLooping;
                _isLooping = value;
                _settingsService.Current.IsLooping = value;
                OnPropertyChanged();
                NotifyLoopDelayVisibilityChanged();
                TryPersistSettingChange(
                    () =>
                    {
                        _isLooping = previousValue;
                        _settingsService.Current.IsLooping = previousValue;
                    },
                    nameof(IsLooping),
                    nameof(ShowFixedLoopDelayInput),
                    nameof(ShowRandomLoopDelayInputs));
            }
        }
    }

    public int LoopCount
    {
        get => _loopCount;
        set
        {
            if (_loopCount != value)
            {
                var previousValue = _loopCount;
                _loopCount = value;
                _settingsService.Current.LoopCount = value;
                OnPropertyChanged();
                TryPersistSettingChange(
                    () =>
                    {
                        _loopCount = previousValue;
                        _settingsService.Current.LoopCount = previousValue;
                    },
                    nameof(LoopCount));
            }
        }
    }

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
                TryPersistSettingChange(
                    () =>
                    {
                        _loopDelayMs = previousValue;
                        _settingsService.Current.LoopDelayMs = previousValue;
                    },
                    nameof(LoopDelayMs));
            }
        }
    }

    public bool UseRandomLoopDelay
    {
        get => _useRandomLoopDelay;
        set
        {
            if (_useRandomLoopDelay == value)
            {
                return;
            }

            var previousUseRandom = _useRandomLoopDelay;
            var previousMin = _loopDelayMinMs ?? 0;
            var previousMax = _loopDelayMaxMs ?? 0;

            _useRandomLoopDelay = value;
            if (value && previousMin == 0 && previousMax == 0)
            {
                var seededDelay = NormalizeDelayInput(LoopDelayMs);
                UpdateLoopDelayRange(seededDelay, seededDelay);
            }

            _settingsService.Current.UseRandomLoopDelay = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LoopDelayMinMs));
            OnPropertyChanged(nameof(LoopDelayMaxMs));
            NotifyLoopDelayVisibilityChanged();

            TryPersistSettingChange(
                () =>
                {
                    _useRandomLoopDelay = previousUseRandom;
                    UpdateLoopDelayRange(previousMin, previousMax);
                    _settingsService.Current.UseRandomLoopDelay = previousUseRandom;
                },
                nameof(UseRandomLoopDelay),
                nameof(LoopDelayMinMs),
                nameof(LoopDelayMaxMs),
                nameof(ShowFixedLoopDelayInput),
                nameof(ShowRandomLoopDelayInputs));
        }
    }

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

            UpdateLoopDelayRange(normalizedMin, normalizedMax);
            OnPropertyChanged();
            if (previousMax != normalizedMax)
            {
                OnPropertyChanged(nameof(LoopDelayMaxMs));
            }

            TryPersistSettingChange(
                () => UpdateLoopDelayRange(previousMin, previousMax),
                nameof(LoopDelayMinMs),
                nameof(LoopDelayMaxMs));
        }
    }

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

            UpdateLoopDelayRange(normalizedMin, normalizedMax);
            OnPropertyChanged();
            if (previousMin != normalizedMin)
            {
                OnPropertyChanged(nameof(LoopDelayMinMs));
            }

            TryPersistSettingChange(
                () => UpdateLoopDelayRange(previousMin, previousMax),
                nameof(LoopDelayMinMs),
                nameof(LoopDelayMaxMs));
        }
    }

    public bool ShowFixedLoopDelayInput => IsLooping && !UseRandomLoopDelay;

    public bool ShowRandomLoopDelayInputs => IsLooping && UseRandomLoopDelay;

    public int? CountdownSeconds
    {
        get => _countdownSeconds;
        set
        {
            if (_countdownSeconds != value)
            {
                var previousValue = _countdownSeconds;
                _countdownSeconds = value;
                _settingsService.Current.CountdownSeconds = value ?? 0;
                OnPropertyChanged();
                TryPersistSettingChange(
                    () =>
                    {
                        _countdownSeconds = previousValue;
                        _settingsService.Current.CountdownSeconds = previousValue ?? 0;
                    },
                    nameof(CountdownSeconds));
            }
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (_isPlaying != value)
            {
                _isPlaying = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanPlayMacro));
                PlaybackStateChanged?.Invoke(this, value);
            }
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (_isPaused != value)
            {
                _isPaused = value;
                OnPropertyChanged();
            }
        }
    }

    public string PlaybackStatus
    {
        get => _playbackStatus;
        private set
        {
            if (_playbackStatus != value)
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

    private bool _canPlayMacroExternal = true;

    /// <summary>
    /// Used by MainWindowViewModel to control if playback can start (considering recording state)
    /// </summary>
    public bool CanPlayMacroExternal
    {
        get => _canPlayMacroExternal;
        set
        {
            if (_canPlayMacroExternal != value)
            {
                _canPlayMacroExternal = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanPlayMacro));
            }
        }
    }

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

        var executionPlan = PlaybackExecutionPlanner.CreatePlan(_loadedMacroSession, _currentMacro);
        if (!string.IsNullOrEmpty(executionPlan.ValidationError))
        {
            PlaybackStatus = executionPlan.ValidationError;
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
        _playbackCts = new CancellationTokenSource();
        _stopRequested = false;
        ResetSequenceState();

        try
        {
            IsPlaying = true;
            IsPaused = false;

            await WaitForCountdownAsync(_playbackCts.Token);
            if (_stopRequested)
            {
                return;
            }

            _statusUpdateTimer?.Start();

            if (executionPlan.UsesSequence)
            {
                await PlaySequentialCycleAsync(sequenceSnapshot, _playbackCts.Token);
            }
            else
            {
                await PlaySingleMacroModeAsync(activeMacro, playbackMode, _playbackCts.Token);
            }

            if (!_stopRequested)
            {
                PlaybackStatus = _localizationService["Playback_StatusComplete"];
            }
        }
        catch (OperationCanceledException) when (_stopRequested)
        {
        }
        catch (Exception ex)
        {
            PlaybackStatus = string.Format(_localizationService.CurrentCulture, _localizationService["Playback_StatusError"], ex.Message);
        }
        finally
        {
            _statusUpdateTimer?.Stop();
            _playbackCts?.Dispose();
            _playbackCts = null;
            ResetSequenceState();
            IsPlaying = false;
            IsPaused = false;
        }
    }

    public void StopPlayback()
    {
        if (!IsPlaying)
        {
            return;
        }

        _stopRequested = true;
        _isWaitingBetweenSequenceCycles = false;
        _statusUpdateTimer?.Stop();
        _playbackCts?.Cancel();
        IsPaused = false;
        _player.Stop();
        PlaybackStatus = _localizationService["Playback_StatusStopped"];

        if (_playbackCts == null)
        {
            IsPlaying = false;
        }
    }

    public void TogglePause()
    {
        if (!IsPlaying || _isWaitingBetweenSequenceCycles || _stopRequested)
        {
            return;
        }

        if (_player.IsPaused)
        {
            _player.Resume();
            IsPaused = false;
            UpdatePlaybackStatus();
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
        _statusUpdateTimer?.Stop();
        if (_statusUpdateTimer != null)
        {
            _statusUpdateTimer.Tick -= OnStatusUpdateTimerTick;
            _statusUpdateTimer = null;
        }

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
            RepeatDelayMaxMs = LoopDelayMaxMs ?? 0
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
            RepeatDelayMaxMs = 0
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
            PlaybackStatus = string.Format(_localizationService.CurrentCulture, _localizationService["Playback_StatusStartingIn"], i);
            await Task.Delay(1000, cancellationToken);
            if (_stopRequested)
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
        UpdatePlaybackStatus();
        await _player.PlayAsync(macro, BuildSingleMacroPlaybackOptions(), cancellationToken);
        if (_stopRequested)
        {
            return;
        }

        if (playbackMode == LoadedMacroPlaybackMode.AdvanceSelection)
        {
            _loadedMacroSession.SelectNext();
        }
    }

    private async Task PlaySequentialCycleAsync(
        IReadOnlyList<LoadedMacroListItem> sequenceSnapshot,
        CancellationToken cancellationToken)
    {
        if (sequenceSnapshot.Count == 0)
        {
            return;
        }

        _isSequencePlayback = true;
        _sequenceMacroCount = sequenceSnapshot.Count;
        _sequenceTotalCycles = IsLooping ? LoopCount : 1;

        var startItemSessionId = sequenceSnapshot[0].SessionId;
        var infiniteCycles = IsLooping && LoopCount == 0;
        var completedCycles = 0;

        try
        {
            while ((infiniteCycles || completedCycles < _sequenceTotalCycles) && !cancellationToken.IsCancellationRequested)
            {
                _sequenceCycle = completedCycles + 1;

                for (var index = 0; index < sequenceSnapshot.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var item = sequenceSnapshot[index];
                    _sequenceMacroIndex = index + 1;
                    _sequenceMacroName = item.Name;
                    _sequenceMacroRepeatCount = item.SequenceRepeatCount;
                    SelectLiveMacroBySessionId(item.SessionId);
                    UpdatePlaybackStatus();

                    await _player.PlayAsync(item.Macro, BuildSequenceMacroPlaybackOptions(item), cancellationToken);
                    if (_stopRequested)
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

                SelectLiveMacroBySessionId(startItemSessionId);
                var cycleDelay = ResolveSequenceCycleDelayMs();
                if (cycleDelay > 0)
                {
                    _isWaitingBetweenSequenceCycles = true;
                    UpdatePlaybackStatus();
                    await Task.Delay(cycleDelay, cancellationToken);
                    _isWaitingBetweenSequenceCycles = false;
                }
            }
        }
        finally
        {
            SelectLiveMacroBySessionId(startItemSessionId);
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

        if (max == int.MaxValue)
        {
            return (int)_random.NextInt64(min, (long)max + 1);
        }

        return _random.Next(min, max + 1);
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
        NotifyPlaybackAvailabilityChanged();
    }

    private void OnLoadedMacroUpdated(object? sender, EventArgs e)
    {
        NotifyPlaybackAvailabilityChanged();
    }

    private void OnLoadedMacroPlaybackModeChanged(object? sender, EventArgs e)
    {
        NotifyPlaybackAvailabilityChanged();
    }

    private void NotifyPlaybackAvailabilityChanged()
    {
        OnPropertyChanged(nameof(HasMacro));
        OnPropertyChanged(nameof(CanPlayMacro));
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

    private void NotifyLoopDelayVisibilityChanged()
    {
        OnPropertyChanged(nameof(ShowFixedLoopDelayInput));
        OnPropertyChanged(nameof(ShowRandomLoopDelayInputs));
    }

    private string GetLoopDelayWaitText()
    {
        if (!UseRandomLoopDelay)
        {
            return $"{LoopDelayMs ?? 0} ms";
        }

        var min = LoopDelayMinMs ?? 0;
        var max = LoopDelayMaxMs ?? 0;
        return min == max ? $"{min} ms" : $"{min}-{max} ms";
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

            Log.Error(ex, "[PlaybackViewModel] Failed to persist playback settings");
            return false;
        }
    }
}
