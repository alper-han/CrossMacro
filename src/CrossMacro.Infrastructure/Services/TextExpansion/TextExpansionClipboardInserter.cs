using System;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;

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

    public async Task<bool> TryInsertAsync(
        IInputSimulator inputSimulator,
        string replacement,
        PasteMethod pasteMethod)
    {
        ArgumentNullException.ThrowIfNull(inputSimulator);
        ArgumentNullException.ThrowIfNull(replacement);

        if (!_clipboardService.IsSupported)
        {
            return false;
        }

        try
        {
            var oldClipboard = await TryBackupClipboardAsync();

            var setTask = _clipboardService.SetTextAsync(replacement);
            if (await Task.WhenAny(setTask, Task.Delay(TextExpansionExecutionTimings.ClipboardWriteTimeout)) != setTask)
            {
                return false;
            }

            await setTask;
            await Task.Delay(TextExpansionExecutionTimings.ClipboardWriteSettleDelay);
            await Task.Delay(TextExpansionExecutionTimings.ClipboardPrePasteDelay);

            await PerformPasteAsync(inputSimulator, pasteMethod);
            await Task.Delay(TextExpansionExecutionTimings.PasteSettleDelay);

            if (oldClipboard is not null)
            {
                _ = RestoreClipboardAsync(oldClipboard, replacement);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Clipboard paste operation failed");
            return false;
        }
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

    private async Task RestoreClipboardAsync(string oldClipboard, string insertedText)
    {
        try
        {
            // Clipboard restore remains best-effort to avoid clobbering a newer user copy.
            await Task.Delay(TextExpansionExecutionTimings.ClipboardRestoreDelay);
            var currentClipboard = await TryReadClipboardAsync();
            if (currentClipboard != null &&
                !string.Equals(currentClipboard, insertedText, StringComparison.Ordinal))
            {
                return;
            }

            var restoreTask = _clipboardService.SetTextAsync(oldClipboard);
            await Task.WhenAny(restoreTask, Task.Delay(TextExpansionExecutionTimings.ClipboardRestoreTimeout));
        }
        catch
        {
            // Clipboard restore is best-effort.
        }
    }

    private async Task<string?> TryReadClipboardAsync()
    {
        try
        {
            return await ReadClipboardWithTimeoutAsync(TextExpansionExecutionTimings.ClipboardRestoreTimeout);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> ReadClipboardWithTimeoutAsync(TimeSpan timeout)
    {
        var getTask = _clipboardService.GetTextAsync();
        if (await Task.WhenAny(getTask, Task.Delay(timeout)) == getTask)
        {
            return await getTask;
        }

        return null;
    }
}
