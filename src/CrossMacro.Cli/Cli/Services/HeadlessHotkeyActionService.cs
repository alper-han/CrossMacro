using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Cli.Services;

public sealed class HeadlessHotkeyActionService : IHeadlessHotkeyActionService
{
    private static readonly ILogger Logger = Log.ForContext<HeadlessHotkeyActionService>();

    private readonly IGlobalHotkeyService _globalHotkeyService;
    private readonly IMacroRecorder _macroRecorder;
    private readonly Func<IMacroPlayer> _macroPlayerFactory;
    private readonly ISettingsService _settingsService;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private bool _isRunning;
    private bool _disposed;
    private bool _playbackPauseHotkeysDisabled;

    private MacroSequence? _lastRecordedMacro;
    private IMacroPlayer? _activePlayer;
    private CancellationTokenSource? _playbackCts;
    private Task? _playbackTask;

    public HeadlessHotkeyActionService(
        IGlobalHotkeyService globalHotkeyService,
        IMacroRecorder macroRecorder,
        Func<IMacroPlayer> macroPlayerFactory,
        ISettingsService settingsService)
    {
        _globalHotkeyService = globalHotkeyService;
        _macroRecorder = macroRecorder;
        _macroPlayerFactory = macroPlayerFactory;
        _settingsService = settingsService;
    }

    public bool IsRunning => _isRunning;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isRunning)
        {
            return;
        }

        _globalHotkeyService.ToggleRecordingRequested += OnToggleRecordingRequested;
        _globalHotkeyService.TogglePlaybackRequested += OnTogglePlaybackRequested;
        _globalHotkeyService.TogglePauseRequested += OnTogglePauseRequested;
        _isRunning = true;

        Logger.Information("[HeadlessHotkeyActionService] Hotkey actions enabled");
    }

    public void Stop()
    {
        if (!_isRunning)
        {
            return;
        }

        _globalHotkeyService.ToggleRecordingRequested -= OnToggleRecordingRequested;
        _globalHotkeyService.TogglePlaybackRequested -= OnTogglePlaybackRequested;
        _globalHotkeyService.TogglePauseRequested -= OnTogglePauseRequested;
        _isRunning = false;

        HandleStopAsync().GetAwaiter().GetResult();
        Logger.Information("[HeadlessHotkeyActionService] Hotkey actions disabled");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _playbackTask?.GetAwaiter().GetResult();
        _gate.Dispose();
    }

    private void OnToggleRecordingRequested(object? sender, EventArgs e)
    {
        ObserveTask(HandleRecordingToggleAsync(), "toggle-recording");
    }

    private void OnTogglePlaybackRequested(object? sender, EventArgs e)
    {
        ObserveTask(HandlePlaybackToggleAsync(), "toggle-playback");
    }

    private void OnTogglePauseRequested(object? sender, EventArgs e)
    {
        ObserveTask(HandlePauseToggleAsync(), "toggle-pause");
    }

    private async Task HandleRecordingToggleAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_isRunning)
            {
                return;
            }

            if (_activePlayer != null)
            {
                Logger.Debug("[HeadlessHotkeyActionService] Recording toggle ignored while playback is active");
                return;
            }

            if (_macroRecorder.IsRecording)
            {
                StopRecordingCore();
                return;
            }

            StartRecordingCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task HandlePlaybackToggleAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_isRunning)
            {
                return;
            }

            if (_macroRecorder.IsRecording)
            {
                Logger.Debug("[HeadlessHotkeyActionService] Playback toggle ignored while recording is active");
                return;
            }

            if (_activePlayer != null)
            {
                _ = StopPlaybackCore();
                Logger.Information("[HeadlessHotkeyActionService] Playback stop requested via hotkey");
                return;
            }

            if (_lastRecordedMacro == null || _lastRecordedMacro.Events.Count == 0)
            {
                Logger.Warning("[HeadlessHotkeyActionService] Playback requested but no recorded macro is available in this headless session");
                return;
            }

            var settings = _settingsService.Current;
            var player = _macroPlayerFactory();
            var cts = new CancellationTokenSource();
            var countdownSeconds = Math.Max(0, settings.CountdownSeconds);
            var options = new PlaybackOptions
            {
                SpeedMultiplier = PlaybackOptions.NormalizeSpeedMultiplier(settings.PlaybackSpeed),
                Loop = settings.IsLooping,
                RepeatCount = settings.LoopCount,
                RepeatDelayMs = settings.LoopDelayMs,
                UseRandomRepeatDelay = settings.UseRandomLoopDelay,
                RepeatDelayMinMs = settings.LoopDelayMinMs,
                RepeatDelayMaxMs = settings.LoopDelayMaxMs
            };

            _activePlayer = player;
            _playbackCts = cts;
            _playbackTask = RunPlaybackAsync(player, _lastRecordedMacro, options, countdownSeconds, cts.Token);
            ObserveTask(_playbackTask, "playback");

            Logger.Information("[HeadlessHotkeyActionService] Playback started via hotkey (Events={EventCount})", _lastRecordedMacro.Events.Count);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task HandlePauseToggleAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_isRunning || _macroRecorder.IsRecording || _activePlayer == null)
            {
                return;
            }

            if (_activePlayer.IsPaused)
            {
                _activePlayer.Resume();
                Logger.Information("[HeadlessHotkeyActionService] Playback resumed via hotkey");
            }
            else
            {
                _activePlayer.Pause();
                Logger.Information("[HeadlessHotkeyActionService] Playback paused via hotkey");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task HandleStopAsync()
    {
        Task? playbackTaskToAwait = null;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            playbackTaskToAwait = StopPlaybackCore();

            if (_macroRecorder.IsRecording)
            {
                StopRecordingCore();
            }
            else
            {
                EnsurePlaybackPauseHotkeysEnabled();
            }
        }
        finally
        {
            _gate.Release();
        }

        if (playbackTaskToAwait != null)
        {
            try
            {
                await playbackTaskToAwait.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private void StartRecordingCore()
    {
        var settings = _settingsService.Current;
        if (!settings.IsMouseRecordingEnabled && !settings.IsKeyboardRecordingEnabled)
        {
            Logger.Warning("[HeadlessHotkeyActionService] Recording toggle ignored because both mouse and keyboard recording are disabled");
            return;
        }

        var forceRelative = settings.ForceRelativeCoordinates && (OperatingSystem.IsLinux() || OperatingSystem.IsWindows());
        var skipInitialZero = forceRelative && settings.SkipInitialZeroZero;
        var ignoredKeys = new[]
        {
            _globalHotkeyService.RecordingHotkeyCode,
            _globalHotkeyService.PlaybackHotkeyCode,
            _globalHotkeyService.PauseHotkeyCode
        };

        try
        {
            _globalHotkeyService.SetPlaybackPauseHotkeysEnabled(false);
            _playbackPauseHotkeysDisabled = true;

            var startTask = _macroRecorder.StartRecordingAsync(
                settings.IsMouseRecordingEnabled,
                settings.IsKeyboardRecordingEnabled,
                ignoredKeys,
                forceRelative: forceRelative,
                skipInitialZero: skipInitialZero,
                cancellationToken: CancellationToken.None);

            _ = startTask.ContinueWith(
                t =>
                {
                    EnsurePlaybackPauseHotkeysEnabled();
                    Logger.Error(t.Exception, "[HeadlessHotkeyActionService] Failed to start recording via hotkey");
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);

            Logger.Information(
                "[HeadlessHotkeyActionService] Recording start requested via hotkey (Mouse={MouseEnabled}, Keyboard={KeyboardEnabled}, ForceRelative={ForceRelative})",
                settings.IsMouseRecordingEnabled,
                settings.IsKeyboardRecordingEnabled,
                forceRelative);
        }
        catch
        {
            EnsurePlaybackPauseHotkeysEnabled();
            throw;
        }
    }

    private void StopRecordingCore()
    {
        try
        {
            var macro = _macroRecorder.StopRecording();
            if (macro == null || macro.Events.Count == 0)
            {
                Logger.Warning("[HeadlessHotkeyActionService] Recording stopped but no events were captured");
                return;
            }

            _lastRecordedMacro = macro;
            Logger.Information("[HeadlessHotkeyActionService] Recording stopped via hotkey (Events={EventCount})", macro.Events.Count);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[HeadlessHotkeyActionService] Failed to stop recording");
        }
        finally
        {
            EnsurePlaybackPauseHotkeysEnabled();
        }
    }

    private Task? StopPlaybackCore()
    {
        var player = _activePlayer;
        var cts = _playbackCts;
        var playbackTask = _playbackTask;

        _activePlayer = null;
        _playbackCts = null;
        _playbackTask = null;

        try
        {
            cts?.Cancel();
        }
        catch
        {
        }
        finally
        {
            cts?.Dispose();
        }

        try
        {
            player?.Stop();
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "[HeadlessHotkeyActionService] Failed to stop active player");
        }

        return playbackTask;
    }

    private async Task RunPlaybackAsync(
        IMacroPlayer player,
        MacroSequence macro,
        PlaybackOptions options,
        int countdownSeconds,
        CancellationToken cancellationToken)
    {
        try
        {
            for (var remaining = countdownSeconds; remaining > 0; remaining--)
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }

            await player.PlayAsync(macro, options, cancellationToken).ConfigureAwait(false);
            Logger.Information("[HeadlessHotkeyActionService] Playback completed via hotkey");
        }
        catch (OperationCanceledException)
        {
            Logger.Information("[HeadlessHotkeyActionService] Playback cancelled via hotkey");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[HeadlessHotkeyActionService] Playback failed via hotkey");
        }
        finally
        {
            try
            {
                player.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "[HeadlessHotkeyActionService] Failed to dispose player");
            }

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (ReferenceEquals(_activePlayer, player))
                {
                    _activePlayer = null;
                    _playbackTask = null;
                    _playbackCts?.Dispose();
                    _playbackCts = null;
                }
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    private void EnsurePlaybackPauseHotkeysEnabled()
    {
        if (!_playbackPauseHotkeysDisabled)
        {
            return;
        }

        try
        {
            _globalHotkeyService.SetPlaybackPauseHotkeysEnabled(true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[HeadlessHotkeyActionService] Failed to re-enable playback/pause hotkeys after recording");
        }
        finally
        {
            _playbackPauseHotkeysDisabled = false;
        }
    }

    private static void ObserveTask(Task task, string operation)
    {
        _ = task.ContinueWith(
            t => Log.Error(t.Exception, "[HeadlessHotkeyActionService] Unhandled error during {Operation}", operation),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
