namespace CrossMacro.UI.ViewModels.Playback;

/// <summary>Plays the loaded macros one after another, cycle by cycle, keeping the live selection and progress up to date.</summary>
internal sealed class PlaybackSequenceRunner(
    IUiDispatcher dispatcher,
    IMacroPlayer player,
    ILoadedMacroSession loadedMacroSession,
    SequencePlaybackProgress progress,
    Func<bool> isStopRequested,
    Action applyStatus,
    Func<LoadedMacroListItem, PlaybackOptions> createOptions,
    Func<int> resolveCycleDelayMs)
{
    internal async Task RunAsync(
        PlaybackSession session,
        IReadOnlyList<LoadedMacroListItem> sequenceSnapshot,
        bool isLooping,
        int loopCount,
        CancellationToken cancellationToken)
    {
        if (sequenceSnapshot.Count is 0)
        {
            return;
        }

        await dispatcher.InvokeAsync(() =>
        {
            progress.IsActive = true;
            progress.MacroCount = sequenceSnapshot.Count;
            progress.TotalCycles = isLooping ? loopCount : 1;
            return Task.CompletedTask;
        }).ConfigureAwait(false);

        var startItemSessionId = sequenceSnapshot[0].SessionId;
        var infiniteCycles = isLooping && loopCount is 0;
        var completedCycles = 0;

        try
        {
            while ((infiniteCycles || completedCycles < progress.TotalCycles) && !cancellationToken.IsCancellationRequested)
            {
                if (!await PlayCycleAsync(session, sequenceSnapshot, completedCycles, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }

                completedCycles++;
                if (!infiniteCycles && completedCycles >= progress.TotalCycles)
                {
                    break;
                }

                await dispatcher.InvokeAsync(() =>
                {
                    SelectLiveMacroBySessionId(startItemSessionId);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
                await WaitBetweenCyclesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await dispatcher.InvokeAsync(() =>
            {
                SelectLiveMacroBySessionId(startItemSessionId);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }
    }

    /// <returns>False when playback was stopped during the cycle.</returns>
    private async Task<bool> PlayCycleAsync(
        PlaybackSession session,
        IReadOnlyList<LoadedMacroListItem> sequenceSnapshot,
        int completedCycles,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < sequenceSnapshot.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = sequenceSnapshot[index];
            await dispatcher.InvokeAsync(() =>
            {
                progress.Cycle = completedCycles + 1;
                progress.MacroIndex = index + 1;
                progress.MacroName = item.Name;
                progress.MacroRepeatCount = item.SequenceRepeatCount;
                SelectLiveMacroBySessionId(item.SessionId);
                applyStatus();
                return Task.CompletedTask;
            }).ConfigureAwait(false);

            await session.PlayAsync(player, item.Macro, createOptions(item)).ConfigureAwait(false);
            if (isStopRequested())
            {
                return false;
            }
        }

        return true;
    }

    private async Task WaitBetweenCyclesAsync(CancellationToken cancellationToken)
    {
        var cycleDelay = resolveCycleDelayMs();
        if (cycleDelay <= 0)
        {
            return;
        }

        await dispatcher.InvokeAsync(() =>
        {
            progress.IsWaitingBetweenCycles = true;
            applyStatus();
            return Task.CompletedTask;
        }).ConfigureAwait(false);
        await Task.Delay(cycleDelay, cancellationToken).ConfigureAwait(false);
        await dispatcher.InvokeAsync(() =>
        {
            progress.IsWaitingBetweenCycles = false;
            return Task.CompletedTask;
        }).ConfigureAwait(false);
    }

    private void SelectLiveMacroBySessionId(Guid sessionId)
    {
        foreach (var item in loadedMacroSession.LoadedMacros)
        {
            if (item.SessionId == sessionId)
            {
                loadedMacroSession.SelectedMacroItem = item;
                return;
            }
        }
    }
}
