namespace CrossMacro.Infrastructure.Services.Automation.Shortcuts;

public sealed partial class ShortcutService
{
    private async Task<bool> ExecuteTaskAsync(
        ShortcutTask task,
        bool requiresHeldHotkey = false,
        bool startReservation = false,
        string? macroFilePath = null,
        bool forceSingleShot = false,
        CancellationToken cancellationToken = default)
    {
        IMacroPlayer? player = null;
        var path = macroFilePath ?? task.MacroFilePath;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (requiresHeldHotkey && !IsHeldHotkeyActive(task.Id))
            {
                return false;
            }

            var macro = await LoadMacroAsync(task, path, requiresHeldHotkey, cancellationToken).ConfigureAwait(false);
            if (macro is null)
            {
                return false;
            }

            _store.Post(() =>
            {
                task.LastStatus = "Running...";
                task.LastTriggeredTime = _timeProvider.GetUtcNow().UtcDateTime;
            });
            ShortcutStarting.RaiseSafely(this, new ShortcutStartingEventArgs(task), nameof(ShortcutService));

            player = _playerFactory();
            if (!TryRegisterActivePlayer(task, player, requiresHeldHotkey, cancellationToken))
            {
                return false;
            }

            var playbackTask = player.PlayAsync(macro, CreatePlaybackOptions(task, forceSingleShot), cancellationToken);
            if (startReservation)
            {
                lock (_lock)
                {
                    _ = _startingTasks.Remove(task.Id);
                }
            }

            await playbackTask.ConfigureAwait(false);

            _store.Post(() =>
            {
                task.LastTriggeredTime = _timeProvider.GetUtcNow().UtcDateTime;
                task.LastStatus = "Success";
            });

            cancellationToken.ThrowIfCancellationRequested();
            ShortcutExecuted.RaiseSafely(this, new ShortcutExecutedEventArgs(task, success: true, "Executed successfully"), nameof(ShortcutService));
            return true;
        }
        catch (PlaybackAlreadyInProgressException)
        {
            ReportOutcome(task, "Skipped (playback busy)", success: false, "Playback was busy");
            return false;
        }
        catch (OperationCanceledException)
        {
            // Playback was stopped/cancelled - this is expected for toggle stop
            ReportOutcome(task, "Stopped", success: true, "Stopped by user");
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ReportOutcome(task, $"Error: {ex.Message}", success: false, ex.Message, updateTriggeredTime: true);
            Log.LogError(ex, "[ShortcutService] Error executing shortcut task {TaskName}", task.Name);
            return false;
        }
        finally
        {
            ReleaseTaskResources(task, player, startReservation);
        }
    }

    private async Task<MacroSequence?> LoadMacroAsync(ShortcutTask task, string? path, bool requiresHeldHotkey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            ReportOutcome(task, "Macro file not found", success: false, "Macro file not found", updateTriggeredTime: true);
            return null;
        }

        var macro = await _fileManager.LoadAsync(path).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (requiresHeldHotkey && !IsHeldHotkeyActive(task.Id))
        {
            return null;
        }

        if (macro is null)
        {
            ReportOutcome(task, "Failed to load macro", success: false, "Failed to load macro", updateTriggeredTime: true);
        }

        return macro;
    }

    /// <summary>Registers the player so a toggle press can stop it; false when a held hotkey was released meanwhile.</summary>
    private bool TryRegisterActivePlayer(ShortcutTask task, IMacroPlayer player, bool requiresHeldHotkey, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (requiresHeldHotkey && !_activeHotkeyKeys.ContainsKey(task.Id))
            {
                return false;
            }

            _activePlayers[task.Id] = player;
            return true;
        }
    }

    private static PlaybackOptions CreatePlaybackOptions(ShortcutTask task, bool forceSingleShot)
    {
        return new PlaybackOptions
        {
            SpeedMultiplier = PlaybackOptions.NormalizeSpeedMultiplier(task.PlaybackSpeed),
            Loop = !forceSingleShot && (task.RunWhileHeld || task.LoopEnabled),
            RepeatCount = ResolveRepeatCount(task, forceSingleShot),
            RepeatDelayMs = task.RepeatDelayMs,
            UseRandomRepeatDelay = task.UseRandomRepeatDelay,
            RepeatDelayMinMs = task.RepeatDelayMinMs,
            RepeatDelayMaxMs = task.RepeatDelayMaxMs,
        };
    }

    private static int ResolveRepeatCount(ShortcutTask task, bool forceSingleShot)
    {
        if (forceSingleShot)
        {
            return 1;
        }

        if (task.RunWhileHeld)
        {
            return 0;
        }

        return task.LoopEnabled ? task.RepeatCount : 1;
    }

    private void ReportOutcome(ShortcutTask task, string status, bool success, string message, bool updateTriggeredTime = false)
    {
        _store.Post(() =>
        {
            task.LastStatus = status;
            if (updateTriggeredTime)
            {
                task.LastTriggeredTime = _timeProvider.GetUtcNow().UtcDateTime;
            }
        });
        ShortcutExecuted.RaiseSafely(this, new ShortcutExecutedEventArgs(task, success, message), nameof(ShortcutService));
    }

    private void ReleaseTaskResources(ShortcutTask task, IMacroPlayer? player, bool startReservation)
    {
        lock (_lock)
        {
            if (player is not null && _activePlayers.TryGetValue(task.Id, out var active) && ReferenceEquals(active, player))
            {
                _ = _activePlayers.Remove(task.Id);
            }
            if (startReservation)
            {
                _ = _startingTasks.Remove(task.Id);
            }
        }
        player?.Dispose();
    }

}
