namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Starts and stops test playback of the whole macro or of the selected actions.</summary>
internal sealed class EditorTestRunner(
    EditorViewModel owner,
    EditorTestPlayback playback,
    EditorMacroBuilder macroBuilder,
    IMacroPlayer macroPlayer,
    IUiDispatcher uiDispatcher)
{
    internal async Task ToggleAsync(IReadOnlyList<EditorAction>? targetActions, bool isSelectedOnly)
    {
        if (playback.IsActive)
        {
            await playback.StopAsync().ConfigureAwait(false);
            return;
        }

        if (macroPlayer.IsPlaying)
        {
            owner.Status = owner.Localize("Editor_StatusOperationBlocked");
            return;
        }

        if (isSelectedOnly && (targetActions is null || targetActions.Count is 0))
        {
            owner.Status = owner.Localize("Editor_StatusSelectActionFirst");
            return;
        }

        var sequence = await macroBuilder.BuildValidAsync(targetActions).ConfigureAwait(false);
        if (sequence is null)
        {
            return;
        }

        await playback.PlayAsync(
            sequence,
            () => uiDispatcher.RunOnUiThreadAsync(() => OnStarted(isSelectedOnly)),
            result => uiDispatcher.RunOnUiThreadAsync(() => OnFinished(isSelectedOnly, result))).ConfigureAwait(false);
    }

    private void OnStarted(bool isSelectedOnly)
    {
        if (owner.IsDisposed)
        {
            return;
        }

        SetRunning(isSelectedOnly, running: true);
        owner.Status = owner.Localize(isSelectedOnly ? "Editor_StatusTestSelectedRunning" : "Editor_StatusTestRunning");
    }

    private void OnFinished(bool isSelectedOnly, EditorTestPlaybackResult result)
    {
        if (owner.IsDisposed)
        {
            return;
        }

        owner.Status = result.Outcome switch
        {
            EditorTestPlaybackOutcome.Completed => owner.Localize(isSelectedOnly ? "Editor_StatusTestSelectedComplete" : "Editor_StatusTestComplete"),
            EditorTestPlaybackOutcome.Cancelled => owner.Localize("Editor_StatusTestCancelled"),
            EditorTestPlaybackOutcome.Failed => owner.LocalizeFormat("Editor_StatusTestError", result.ErrorMessage ?? string.Empty),
            _ => owner.Status,
        };
        SetRunning(isSelectedOnly, running: false);
    }

    private void SetRunning(bool isSelectedOnly, bool running)
    {
        if (isSelectedOnly)
        {
            owner.IsRunningSelectedTest = running;
        }
        else
        {
            owner.IsRunningTest = running;
        }
    }
}
