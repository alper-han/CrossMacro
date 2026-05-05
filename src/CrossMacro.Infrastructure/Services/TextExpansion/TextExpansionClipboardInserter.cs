using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.TextExpansion;

internal sealed class TextExpansionClipboardInserter
{
    private readonly IClipboardService _clipboardService;
    private readonly TextExpansionKeyDispatcher _keyDispatcher;

    public TextExpansionClipboardInserter(
        IClipboardService clipboardService,
        TextExpansionKeyDispatcher keyDispatcher)
    {
        ArgumentNullException.ThrowIfNull(clipboardService);
        ArgumentNullException.ThrowIfNull(keyDispatcher);

        _clipboardService = clipboardService;
        _keyDispatcher = keyDispatcher;
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
            var oldClipboard = await TryBackupClipboardAsync();

            var wroteReplacement = await TryWriteClipboardAsync(
                replacement,
                TextExpansionExecutionTimings.ClipboardWriteTimeout);
            if (!wroteReplacement)
            {
                return null;
            }

            await Task.Delay(TextExpansionExecutionTimings.ClipboardWriteSettleDelay);
            if (!await VerifyClipboardContainsReplacementAsync(replacement))
            {
                if (oldClipboard is not null)
                {
                    await RestoreClipboardAsync(oldClipboard, replacement);
                }

                return null;
            }

            return new PreparedClipboardPaste(oldClipboard, replacement);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Clipboard paste preparation failed");
            return null;
        }
    }

    public async Task CommitAsync(
        IInputSimulator inputSimulator,
        PreparedClipboardPaste preparedPaste,
        PasteMethod pasteMethod)
    {
        ArgumentNullException.ThrowIfNull(inputSimulator);
        ArgumentNullException.ThrowIfNull(preparedPaste);

        await Task.Delay(TextExpansionExecutionTimings.ClipboardPrePasteDelay);
        await PerformPasteAsync(inputSimulator, pasteMethod);
        await Task.Delay(TextExpansionExecutionTimings.PasteSettleDelay);
    }

    public async Task RestoreAsync(PreparedClipboardPaste preparedPaste)
    {
        ArgumentNullException.ThrowIfNull(preparedPaste);

        if (preparedPaste.OldClipboard is null)
        {
            return;
        }

        await RestoreClipboardAsync(preparedPaste.OldClipboard, preparedPaste.InsertedText);
    }

    private async Task<string?> TryBackupClipboardAsync()
    {
        try
        {
            return await ReadClipboardWithTimeoutAsync(TextExpansionExecutionTimings.ClipboardBackupReadTimeout);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to backup clipboard");
            return null;
        }
    }

    private async Task PerformPasteAsync(IInputSimulator inputSimulator, PasteMethod pasteMethod)
    {
        switch (pasteMethod)
        {
            case PasteMethod.CtrlShiftV:
                await _keyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_V, shift: true, ctrl: true);
                break;
            case PasteMethod.ShiftInsert:
                await _keyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_INSERT, shift: true);
                break;
            case PasteMethod.CtrlV:
            default:
                await _keyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_V, ctrl: true);
                break;
        }
    }

    private async Task<bool> VerifyClipboardContainsReplacementAsync(string replacement)
    {
        var currentClipboard = await ReadClipboardWithTimeoutAsync(TextExpansionExecutionTimings.ClipboardVerifyTimeout);
        if (string.Equals(currentClipboard, replacement, StringComparison.Ordinal))
        {
            return true;
        }

        Log.Warning("Clipboard verification failed; paste skipped to avoid inserting stale clipboard content");
        return false;
    }

    private async Task RestoreClipboardAsync(string oldClipboard, string insertedText)
    {
        try
        {
            // Clipboard restore remains best-effort to avoid clobbering a newer user copy.
            await Task.Delay(TextExpansionExecutionTimings.ClipboardRestoreDelay);
            var currentClipboard = await TryReadClipboardAsync();
            if (!string.Equals(currentClipboard, insertedText, StringComparison.Ordinal))
            {
                return;
            }

            await TryWriteClipboardAsync(oldClipboard, TextExpansionExecutionTimings.ClipboardRestoreTimeout);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Clipboard restore skipped after best-effort failure");
            // Clipboard restore is best-effort.
        }
    }

    private async Task<string?> TryReadClipboardAsync()
    {
        try
        {
            return await ReadClipboardWithTimeoutAsync(TextExpansionExecutionTimings.ClipboardRestoreTimeout);
        }
        catch (Exception ex)
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
            if (await Task.WhenAny(readTask, Task.Delay(timeout)) == readTask)
            {
                try
                {
                    return await readTask;
                }
                finally
                {
                    timeoutSource.Dispose();
                }
            }

            timeoutSource.Cancel();
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
        catch
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
                await _clipboardService.SetTextAsync(text, token);
                return true;
            },
            timeoutSource.Token);

        try
        {
            if (await Task.WhenAny(writeTask, Task.Delay(timeout)) == writeTask)
            {
                try
                {
                    return await writeTask;
                }
                finally
                {
                    timeoutSource.Dispose();
                }
            }

            timeoutSource.Cancel();
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
        catch
        {
            timeoutSource.Dispose();
            throw;
        }
    }

    private static Task<T> RunClipboardOperationAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => operation(cancellationToken), CancellationToken.None);
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
