
namespace CrossMacro.Infrastructure.Services.TextExpansion;

internal sealed class TextExpansionClipboardInserter
{
    private readonly IClipboardService _clipboardService;
    private readonly TimeProvider _timeProvider;
    public TextExpansionClipboardInserter(
        IClipboardService clipboardService,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(clipboardService);

        _clipboardService = clipboardService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsSupported => _clipboardService.IsSupported;

    public async Task<PreparedClipboardPaste?> TryPrepareAsync(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        if (!_clipboardService.IsSupported)
        {
            return null;
        }

        try
        {
            var backup = await TryBackupClipboardAsync().ConfigureAwait(false);
            if (!backup.Completed)
            {
                Log.Warning("Clipboard content could not be read; skipping paste mode so the replacement is never left on the clipboard");
                return null;
            }

            var oldClipboard = backup.Text;

            var wroteReplacement = await TryWriteClipboardAsync(
                replacement,
                TextExpansionExecutionTimings.ClipboardWriteTimeout).ConfigureAwait(false);
            if (!wroteReplacement)
            {
                return null;
            }

            if (_clipboardService is not IClipboardWriteReadbackCapability
                {
                    GuaranteesImmediateReadback: true,
                }
                && !await VerifyClipboardContainsReplacementAsync(replacement).ConfigureAwait(false))
            {
                await RestoreClipboardAsync(oldClipboard, replacement).ConfigureAwait(false);

                return null;
            }

            return new PreparedClipboardPaste(oldClipboard, replacement);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Clipboard paste preparation failed");
            return null;
        }
    }

    public async Task CommitAsync(
        IInputSimulator inputSimulator,
        PreparedClipboardPaste preparedPaste,
        PasteMethod pasteMethod,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputSimulator);
        ArgumentNullException.ThrowIfNull(preparedPaste);

        await PerformPasteAsync(inputSimulator, pasteMethod, cancellationToken).ConfigureAwait(false);
        await Task.Delay(TextExpansionExecutionTimings.PasteSettleDelay, _timeProvider, cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreAsync(PreparedClipboardPaste preparedPaste)
    {
        ArgumentNullException.ThrowIfNull(preparedPaste);

        await RestoreClipboardAsync(preparedPaste.OldClipboard, preparedPaste.InsertedText).ConfigureAwait(false);
    }

    private async Task<ClipboardRead> TryBackupClipboardAsync()
    {
        try
        {
            return await ReadClipboardWithTimeoutAsync(TextExpansionExecutionTimings.ClipboardBackupReadTimeout).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "Failed to backup clipboard");
            return ClipboardRead.Failed;
        }
    }

    private async Task PerformPasteAsync(IInputSimulator inputSimulator, PasteMethod pasteMethod, CancellationToken cancellationToken)
    {
        switch (pasteMethod)
        {
            case PasteMethod.CtrlShiftV:
                await TextExpansionKeyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_V, shift: true, ctrl: true, cancellationToken: cancellationToken, timeProvider: _timeProvider).ConfigureAwait(false);
                break;
            case PasteMethod.ShiftInsert:
                await TextExpansionKeyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_INSERT, shift: true, cancellationToken: cancellationToken, timeProvider: _timeProvider).ConfigureAwait(false);
                break;
            default:
                await SendStandardPasteAsync(inputSimulator, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task SendStandardPasteAsync(IInputSimulator inputSimulator, CancellationToken cancellationToken)
    {
        if (inputSimulator is IPlatformPasteShortcutProvider { UsesMetaKeyForStandardPaste: true })
        {
            await TextExpansionKeyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_V, meta: true, cancellationToken: cancellationToken, timeProvider: _timeProvider).ConfigureAwait(false);
            return;
        }

        await TextExpansionKeyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_V, ctrl: true, cancellationToken: cancellationToken, timeProvider: _timeProvider).ConfigureAwait(false);
    }

    private async Task<bool> VerifyClipboardContainsReplacementAsync(string replacement)
    {
        var startedAt = _timeProvider.GetTimestamp();
        if (await ClipboardContainsReplacementAsync(replacement, TextExpansionExecutionTimings.ClipboardVerifyTimeout).ConfigureAwait(false))
        {
            return true;
        }

        var remaining = TextExpansionExecutionTimings.ClipboardVerifyTimeout - _timeProvider.GetElapsedTime(startedAt);
        if (remaining > TimeSpan.Zero)
        {
            var retryDelay = remaining < TextExpansionExecutionTimings.ClipboardWriteSettleDelay
                ? remaining
                : TextExpansionExecutionTimings.ClipboardWriteSettleDelay;
            await Task.Delay(retryDelay, _timeProvider, CancellationToken.None).ConfigureAwait(false);
            remaining = TextExpansionExecutionTimings.ClipboardVerifyTimeout - _timeProvider.GetElapsedTime(startedAt);
            if (remaining > TimeSpan.Zero && await ClipboardContainsReplacementAsync(replacement, remaining).ConfigureAwait(false))
            {
                return true;
            }
        }

        Log.Warning("Clipboard verification failed; paste skipped to avoid inserting stale clipboard content");
        return false;
    }

    private async Task<bool> ClipboardContainsReplacementAsync(string replacement, TimeSpan timeout)
    {
        var currentClipboard = await ReadClipboardWithTimeoutAsync(timeout).ConfigureAwait(false);
        return currentClipboard.Completed && string.Equals(currentClipboard.Text, replacement, StringComparison.Ordinal);
    }

    /// <summary>
    /// Puts the clipboard back the way it was before the paste. A clipboard that held no text is cleared,
    /// so the inserted text never outlives the expansion. Restoring is skipped when the user copied
    /// something newer in the meantime.
    /// </summary>
    private async Task RestoreClipboardAsync(string? oldClipboard, string insertedText)
    {
        try
        {
            var currentClipboard = await TryReadClipboardAsync().ConfigureAwait(false);
            if (!string.Equals(currentClipboard, insertedText, StringComparison.Ordinal))
            {
                return;
            }

            _ = await TryWriteClipboardAsync(oldClipboard ?? string.Empty, TextExpansionExecutionTimings.ClipboardRestoreTimeout).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "Clipboard restore skipped after best-effort failure");
        }
    }

    private async Task<string?> TryReadClipboardAsync()
    {
        try
        {
            return (await ReadClipboardWithTimeoutAsync(TextExpansionExecutionTimings.ClipboardRestoreTimeout).ConfigureAwait(false)).Text;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "Clipboard read skipped after best-effort failure");
            return null;
        }
    }

    private async Task<ClipboardRead> ReadClipboardWithTimeoutAsync(TimeSpan timeout)
    {
        var read = await RunWithTimeoutAsync(token => _clipboardService.GetTextAsync(token), timeout, "read").ConfigureAwait(false);
        return new ClipboardRead(read.Completed, read.Value);
    }

    private async Task<bool> TryWriteClipboardAsync(string text, TimeSpan timeout)
    {
        var write = await RunWithTimeoutAsync(
            async token =>
            {
                await _clipboardService.SetTextAsync(text, token).ConfigureAwait(false);
                return true;
            },
            timeout,
            "write").ConfigureAwait(false);
        return write.Completed && write.Value;
    }

    private async Task<TimedOperation<T>> RunWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        string operationName)
    {
        var timeoutSource = new CancellationTokenSource();
        // The timeout must also cover implementations that perform synchronous work
        // before returning their Task (for example, lazy STA initialization).
        var operationTask = Task.Run(() => operation(timeoutSource.Token), timeoutSource.Token);

        try
        {
            if (await Task.WhenAny(operationTask, Task.Delay(timeout, _timeProvider, timeoutSource.Token)).ConfigureAwait(false) == operationTask)
            {
                try
                {
                    return new TimedOperation<T>(Completed: true, await operationTask.ConfigureAwait(false));
                }
                finally
                {
                    timeoutSource.Dispose();
                }
            }

            await timeoutSource.CancelAsync().ConfigureAwait(false);
            ObserveTimedOutOperation(operationTask, timeoutSource);
            Log.Warning("Clipboard {Operation} timed out after {TimeoutMs}ms", operationName, timeout.TotalMilliseconds);
            return default;
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            timeoutSource.Dispose();
            Log.Warning("Clipboard {Operation} timed out after {TimeoutMs}ms", operationName, timeout.TotalMilliseconds);
            return default;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            timeoutSource.Dispose();
            throw;
        }
    }

    private static void ObserveTimedOutOperation(Task operation, CancellationTokenSource timeoutSource)
    {
        _ = operation.ContinueWith(
            completedTask =>
            {
                _ = completedTask.Exception;
                timeoutSource.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private readonly record struct TimedOperation<T>(bool Completed, T? Value);

    private readonly record struct ClipboardRead(bool Completed, string? Text)
    {
        public static ClipboardRead Failed => default;
    }

    internal sealed record PreparedClipboardPaste(string? OldClipboard, string InsertedText);
}
