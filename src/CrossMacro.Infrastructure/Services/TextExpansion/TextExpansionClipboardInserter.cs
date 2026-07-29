
namespace CrossMacro.Infrastructure.Services.TextExpansion;

internal sealed class TextExpansionClipboardInserter
{
    private readonly IClipboardService _clipboardService;
    public TextExpansionClipboardInserter(
        IClipboardService clipboardService)
    {
        ArgumentNullException.ThrowIfNull(clipboardService);

        _clipboardService = clipboardService;
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
            var oldClipboard = await TryBackupClipboardAsync().ConfigureAwait(false);

            var wroteReplacement = await TryWriteClipboardAsync(
                replacement,
                TextExpansionExecutionTimings.ClipboardWriteTimeout).ConfigureAwait(false);
            if (!wroteReplacement)
            {
                return null;
            }

            if (!await VerifyClipboardContainsReplacementAsync(replacement).ConfigureAwait(false))
            {
                if (oldClipboard is not null)
                {
                    await RestoreClipboardAsync(oldClipboard, replacement).ConfigureAwait(false);
                }

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

    public static async Task CommitAsync(
        IInputSimulator inputSimulator,
        PreparedClipboardPaste preparedPaste,
        PasteMethod pasteMethod,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputSimulator);
        ArgumentNullException.ThrowIfNull(preparedPaste);

        await Task.Delay(TextExpansionExecutionTimings.ClipboardPrePasteDelay, TimeProvider.System, cancellationToken).ConfigureAwait(false);
        await PerformPasteAsync(inputSimulator, pasteMethod, cancellationToken).ConfigureAwait(false);
        await Task.Delay(TextExpansionExecutionTimings.PasteSettleDelay, TimeProvider.System, cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreAsync(PreparedClipboardPaste preparedPaste)
    {
        ArgumentNullException.ThrowIfNull(preparedPaste);

        if (preparedPaste.OldClipboard is null)
        {
            return;
        }

        await RestoreClipboardAsync(preparedPaste.OldClipboard, preparedPaste.InsertedText).ConfigureAwait(false);
    }

    private async Task<string?> TryBackupClipboardAsync()
    {
        try
        {
            return await ReadClipboardWithTimeoutAsync(TextExpansionExecutionTimings.ClipboardBackupReadTimeout).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "Failed to backup clipboard");
            return null;
        }
    }

    private static async Task PerformPasteAsync(IInputSimulator inputSimulator, PasteMethod pasteMethod, CancellationToken cancellationToken)
    {
        switch (pasteMethod)
        {
            case PasteMethod.CtrlShiftV:
                await TextExpansionKeyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_V, shift: true, ctrl: true, cancellationToken: cancellationToken).ConfigureAwait(false);
                break;
            case PasteMethod.ShiftInsert:
                await TextExpansionKeyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_INSERT, shift: true, cancellationToken: cancellationToken).ConfigureAwait(false);
                break;
            default:
                await SendStandardPasteAsync(inputSimulator, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private static async Task SendStandardPasteAsync(IInputSimulator inputSimulator, CancellationToken cancellationToken)
    {
        if (inputSimulator is IPlatformPasteShortcutProvider { UsesMetaKeyForStandardPaste: true })
        {
            await TextExpansionKeyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_V, meta: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        await TextExpansionKeyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_V, ctrl: true, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> VerifyClipboardContainsReplacementAsync(string replacement)
    {
        var startedAt = Stopwatch.GetTimestamp();
        if (await ClipboardContainsReplacementAsync(replacement, TextExpansionExecutionTimings.ClipboardVerifyTimeout).ConfigureAwait(false))
        {
            return true;
        }

        var remaining = TextExpansionExecutionTimings.ClipboardVerifyTimeout - Stopwatch.GetElapsedTime(startedAt);
        if (remaining > TimeSpan.Zero)
        {
            var retryDelay = remaining < TextExpansionExecutionTimings.ClipboardWriteSettleDelay
                ? remaining
                : TextExpansionExecutionTimings.ClipboardWriteSettleDelay;
            await Task.Delay(retryDelay, TimeProvider.System, CancellationToken.None).ConfigureAwait(false);
            remaining = TextExpansionExecutionTimings.ClipboardVerifyTimeout - Stopwatch.GetElapsedTime(startedAt);
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
        if (string.Equals(currentClipboard, replacement, StringComparison.Ordinal))
        {
            return true;
        }
        return false;
    }

    private async Task RestoreClipboardAsync(string oldClipboard, string insertedText)
    {
        try
        {
            // Clipboard restore remains best-effort to avoid clobbering a newer user copy.
            await Task.Delay(TextExpansionExecutionTimings.ClipboardRestoreDelay, TimeProvider.System, CancellationToken.None).ConfigureAwait(false);
            var currentClipboard = await TryReadClipboardAsync().ConfigureAwait(false);
            if (!string.Equals(currentClipboard, insertedText, StringComparison.Ordinal))
            {
                return;
            }

            _ = await TryWriteClipboardAsync(oldClipboard, TextExpansionExecutionTimings.ClipboardRestoreTimeout).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "Clipboard restore skipped after best-effort failure");
            // Clipboard restore is best-effort.
        }
    }

    private async Task<string?> TryReadClipboardAsync()
    {
        try
        {
            return await ReadClipboardWithTimeoutAsync(TextExpansionExecutionTimings.ClipboardRestoreTimeout).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "Clipboard read skipped after best-effort failure");
            return null;
        }
    }

    private async Task<string?> ReadClipboardWithTimeoutAsync(TimeSpan timeout)
    {
        var timeoutSource = new CancellationTokenSource();
        var readTask = RunClipboardOperationAsync(
            token => _clipboardService.GetTextAsync(token),
            timeoutSource.Token);

        try
        {
            if (await Task.WhenAny(readTask, Task.Delay(timeout, TimeProvider.System, timeoutSource.Token)).ConfigureAwait(false) == readTask)
            {
                try
                {
                    return await readTask.ConfigureAwait(false);
                }
                finally
                {
                    timeoutSource.Dispose();
                }
            }

            await timeoutSource.CancelAsync().ConfigureAwait(false);
            ObserveTimedOutOperation(readTask, timeoutSource);
            Log.Warning("Clipboard read timed out after {TimeoutMs}ms", timeout.TotalMilliseconds);
            return null;
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            timeoutSource.Dispose();
            Log.Warning("Clipboard read timed out after {TimeoutMs}ms", timeout.TotalMilliseconds);
            return null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            timeoutSource.Dispose();
            throw;
        }
    }

    private async Task<bool> TryWriteClipboardAsync(string text, TimeSpan timeout)
    {
        var timeoutSource = new CancellationTokenSource();
        var writeTask = RunClipboardOperationAsync(
            async token =>
            {
                await _clipboardService.SetTextAsync(text, token).ConfigureAwait(false);
                return true;
            },
            timeoutSource.Token);

        try
        {
            if (await Task.WhenAny(writeTask, Task.Delay(timeout, TimeProvider.System, timeoutSource.Token)).ConfigureAwait(false) == writeTask)
            {
                try
                {
                    return await writeTask.ConfigureAwait(false);
                }
                finally
                {
                    timeoutSource.Dispose();
                }
            }

            await timeoutSource.CancelAsync().ConfigureAwait(false);
            ObserveTimedOutOperation(writeTask, timeoutSource);
            Log.Warning("Clipboard write timed out after {TimeoutMs}ms", timeout.TotalMilliseconds);
            return false;
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            timeoutSource.Dispose();
            Log.Warning("Clipboard write timed out after {TimeoutMs}ms", timeout.TotalMilliseconds);
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            timeoutSource.Dispose();
            throw;
        }
    }

    private static Task<T> RunClipboardOperationAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        // Offload to the thread pool: some IClipboardService implementations block synchronously
        // before returning a Task, which would bypass the timeout entirely.
        return Task.Run(() => operation(cancellationToken), cancellationToken);
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

    internal sealed record PreparedClipboardPaste(string? OldClipboard, string InsertedText);
}
