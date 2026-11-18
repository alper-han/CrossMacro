namespace CrossMacro.UI.ViewModels.Playback;

/// <summary>
/// Asks the user to confirm loop settings that repeat faster than the warning threshold. Concurrent callers
/// share one open dialog; an acknowledgement lasts until the settings become safe again.
/// </summary>
internal sealed class FastLoopWarningGuard(
    IUiDispatcher dispatcher,
    IDialogService dialogService,
    ILocalizationService localizationService,
    Func<bool> isRisky,
    Func<bool> isSuppressed,
    Action suppress,
    Func<bool> isDisposed)
{
    private bool _acknowledged;
    private Task? _warningTask;

    /// <summary>Forgets an earlier acknowledgement once the loop settings are no longer risky.</summary>
    internal void ResetAcknowledgementWhenSafe()
    {
        if (!isRisky())
        {
            _acknowledged = false;
        }
    }

    internal async Task<bool> ConfirmAsync(bool forPlayback)
    {
        var confirmed = false;
        await dispatcher.InvokeAsync(async () =>
        {
            confirmed = await ConfirmOnUiThreadAsync(forPlayback).ConfigureAwait(true);
        }).ConfigureAwait(false);
        return confirmed;
    }

    private async Task<bool> ConfirmOnUiThreadAsync(bool forPlayback)
    {
        if (!isRisky() || _acknowledged || isSuppressed())
        {
            return true;
        }

        var warningTask = _warningTask;
        if (warningTask is null)
        {
            warningTask = ShowWarningAsync(forPlayback);
            _warningTask = warningTask;
            try
            {
                await warningTask.ConfigureAwait(true);
            }
            finally
            {
                _warningTask = null;
            }
        }
        else
        {
            await warningTask.ConfigureAwait(true);
        }

        return !isRisky() || _acknowledged || isSuppressed();
    }

    private async Task ShowWarningAsync(bool forPlayback)
    {
        var result = await dialogService.ShowFastLoopWarningAsync(
            localizationService["Playback_FastLoopWarningTitle"],
            localizationService["Playback_FastLoopWarningMessage"],
            localizationService[forPlayback ? "Playback_FastLoopWarningPlay" : "Playback_FastLoopWarningContinue"],
            localizationService[forPlayback ? "Playback_FastLoopWarningAbort" : "Playback_FastLoopWarningCancel"],
            localizationService["Playback_FastLoopWarningSuppress"]).ConfigureAwait(true);
        if (isDisposed() || !result.ContinuePlayback)
        {
            return;
        }

        _acknowledged = true;
        if (result.SuppressFutureWarnings)
        {
            suppress();
        }
    }
}
