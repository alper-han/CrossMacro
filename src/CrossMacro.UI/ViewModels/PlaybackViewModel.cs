using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Playback tab - handles macro playback functionality
/// </summary>
public class PlaybackViewModel : ViewModelBase
{
    private readonly IMacroPlayer _player;
    private readonly ISettingsService _settingsService;
    
    private double _playbackSpeed = 1.0;
    private bool _isLooping;
    private int _loopCount = 1;
    private int? _loopDelayMs = 0;
    private int? _countdownSeconds = 0;
    private bool _isPlaying;
    private bool _isPaused;
    private string _playbackStatus = "Ready";
    
    private MacroSequence? _currentMacro;
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
        ISettingsService settingsService)
    {
        _player = player;
        _settingsService = settingsService;
        
        // Initialize playback settings from saved settings
        _playbackSpeed = _settingsService.Current.PlaybackSpeed;
        _isLooping = _settingsService.Current.IsLooping;
        _loopCount = _settingsService.Current.LoopCount;
        _loopDelayMs = _settingsService.Current.LoopDelayMs;
        _countdownSeconds = _settingsService.Current.CountdownSeconds;
        
        // Setup status update timer
        _statusUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _statusUpdateTimer.Tick += OnStatusUpdateTimerTick;
    }
    
    private void OnStatusUpdateTimerTick(object? sender, EventArgs e)
    {
        if (IsPlaying && !IsPaused)
        {
            UpdatePlaybackStatus();
        }
    }
    
    private void UpdatePlaybackStatus()
    {
        var currentLoop = _player.CurrentLoop;
        var totalLoops = _player.TotalLoops;
        var isWaiting = _player.IsWaitingBetweenLoops;
        
        // If waiting between loops, show delay message
        if (isWaiting)
        {
            var delayMs = LoopDelayMs ?? 0;
            PlaybackStatus = $"Waiting {delayMs} ms before next loop...";
            return;
        }
        
        // Otherwise show normal playing status
        if (totalLoops == 0)
        {
            PlaybackStatus = $"Playing (Loop {currentLoop} - Infinite)";
        }
        else if (totalLoops > 1)
        {
            PlaybackStatus = $"Playing (Loop {currentLoop}/{totalLoops})";
        }
        else
        {
            PlaybackStatus = "Playing...";
        }
    }
    
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            if (Math.Abs(_playbackSpeed - value) > 0.01)
            {
                _playbackSpeed = value;
                _settingsService.Current.PlaybackSpeed = value;
                OnPropertyChanged();
                _ = _settingsService.SaveAsync();
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
                _isLooping = value;
                _settingsService.Current.IsLooping = value;
                OnPropertyChanged();
                _ = _settingsService.SaveAsync();
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
                _loopCount = value;
                _settingsService.Current.LoopCount = value;
                OnPropertyChanged();
                _ = _settingsService.SaveAsync();
            }
        }
    }
    
    public int? LoopDelayMs
    {
        get => _loopDelayMs;
        set
        {
            if (_loopDelayMs != value)
            {
                _loopDelayMs = value;
                _settingsService.Current.LoopDelayMs = value ?? 0;
                OnPropertyChanged();
                _ = _settingsService.SaveAsync();
            }
        }
    }
    
    public int? CountdownSeconds
    {
        get => _countdownSeconds;
        set
        {
            if (_countdownSeconds != value)
            {
                _countdownSeconds = value;
                _settingsService.Current.CountdownSeconds = value ?? 0;
                OnPropertyChanged();
                _ = _settingsService.SaveAsync();
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
    
    public bool HasMacro => _currentMacro != null && _currentMacro.EventCount > 0;
    
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
    /// Set the macro to be played
    /// </summary>
    public void SetMacro(MacroSequence? macro)
    {
        _currentMacro = macro;
        OnPropertyChanged(nameof(HasMacro));
        OnPropertyChanged(nameof(CanPlayMacro));
    }
    
    public async Task PlayMacroAsync()
    {
        if (_currentMacro == null || IsPlaying || !CanPlayMacroExternal)
            return;
        
        try
        {
            IsPlaying = true;
            IsPaused = false; // Reset pause state for new playback
            
            // Countdown
            var countdown = CountdownSeconds ?? 0;
            if (countdown > 0)
            {
                for (int i = countdown; i > 0; i--)
                {
                    PlaybackStatus = $"Starting in {i}...";
                    await Task.Delay(1000);
                    if (!IsPlaying) return; // Cancelled via Stop
                }
            }
            
            // Start status update timer AFTER countdown
            _statusUpdateTimer?.Start();
            
            UpdatePlaybackStatus();
            
            var options = new PlaybackOptions
            {
                SpeedMultiplier = PlaybackSpeed,
                Loop = IsLooping,
                RepeatCount = LoopCount,
                RepeatDelayMs = LoopDelayMs ?? 0
            };
            
            await _player.PlayAsync(_currentMacro, options);
            
            PlaybackStatus = "Playback complete";
        }
        catch (Exception ex)
        {
            PlaybackStatus = $"Playback error: {ex.Message}";
        }
        finally
        {
            _statusUpdateTimer?.Stop();
            IsPlaying = false;
            IsPaused = false;
        }
    }
    
    public void StopPlayback()
    {
        if (IsPlaying)
        {
            IsPlaying = false;
            IsPaused = false;
            _player.Stop();
            PlaybackStatus = "Playback stopped";
        }
    }
    
    public void TogglePause()
    {
        if (!IsPlaying) return;

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
            PlaybackStatus = "Paused";
        }
    }
    
    /// <summary>
    /// Toggle playback state (for hotkey handling)
    /// </summary>
    public void TogglePlayback()
    {
        if (IsPlaying)
            StopPlayback();
        else if (CanPlayMacro && CanPlayMacroExternal)
            _ = PlayMacroAsync();
    }
}
