namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class MacroPlaybackLifecycle(
    Action<CancellationToken> begin,
    Func<CancellationToken> getSessionToken,
    Func<Task> cleanupAsync,
    Action<int, int> setLoopProgress,
    Action<bool> setWaitingBetweenLoops,
    Func<MacroSequence, bool> hasOnlyRuntimeScriptSteps,
    Func<MacroSequence, bool> hasRuntimeScriptSteps,
    Func<MacroSequence, CancellationToken, Task> executeScreenReadScriptStepsAsync,
    Func<MacroSequence, CancellationToken, Task> setupRuntimeScriptOnlyAsync,
    Func<MacroSequence, CancellationToken, Task> setupPlaybackAsync,
    Func<int, MacroSequence, CancellationToken, Task> prepareIterationAsync,
    Action<int> logIterationStart,
    Action<PlaybackOptions, int, bool> logLoopSettings,
    Func<MacroSequence, double, PlaybackOptions, CancellationToken, Task> playOnceAsync,
    Func<MacroSequence, double, PlaybackOptions, CancellationToken, Task> playOnceRuntimeScriptAsync,
    Func<CancellationToken, Task> waitForStabilizationAsync,
    Func<MacroSequence, long> resolveTrailingDelayMicroseconds,
    Func<PlaybackOptions, int> resolveRepeatDelayMs,
    Func<double, CancellationToken, Task> waitAsync)
{
    private const int IterationYieldInterval = 50;

    public async Task RunAsync(MacroSequence macro, PlaybackOptions options, CancellationToken cancellationToken)
    {
        double normalizedSpeed = PlaybackOptions.NormalizeSpeedMultiplier(options.SpeedMultiplier);
        int repeatCount = options.Loop ? options.RepeatCount : 1;
        bool infiniteLoop = options.Loop && repeatCount is 0;
        setLoopProgress(infiniteLoop ? 0 : repeatCount, 1);

        begin(cancellationToken);
        var sessionToken = getSessionToken();

        try
        {
            if (macro.Events.Count is 0 && hasOnlyRuntimeScriptSteps(macro))
            {
                await RunRuntimeScriptOnlyAsync(macro, options, normalizedSpeed, repeatCount, infiniteLoop, sessionToken).ConfigureAwait(false);
                return;
            }

            if (macro.Events.Count is 0 && !hasRuntimeScriptSteps(macro))
            {
                await executeScreenReadScriptStepsAsync(macro, sessionToken).ConfigureAwait(false);
                return;
            }

            await setupPlaybackAsync(macro, sessionToken).ConfigureAwait(false);
            logLoopSettings(options, repeatCount, infiniteLoop);
            await waitForStabilizationAsync(sessionToken).ConfigureAwait(false);
            sessionToken.ThrowIfCancellationRequested();

            var iteration = 0;
            while ((infiniteLoop || iteration < repeatCount) && !sessionToken.IsCancellationRequested)
            {
                setLoopProgress(infiniteLoop ? 0 : repeatCount, iteration + 1);
                logIterationStart(iteration + 1);

                if (iteration > 0)
                {
                    await prepareIterationAsync(iteration, macro, sessionToken).ConfigureAwait(false);
                }

                if (hasRuntimeScriptSteps(macro))
                {
                    await playOnceRuntimeScriptAsync(macro, normalizedSpeed, options, sessionToken).ConfigureAwait(false);
                }
                else
                {
                    await playOnceAsync(macro, normalizedSpeed, options, sessionToken).ConfigureAwait(false);
                }

                await WaitForNextIterationAsync(macro, options, normalizedSpeed, iteration, repeatCount, infiniteLoop, sessionToken).ConfigureAwait(false);
                iteration++;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // StopPlayback cancels the internal session without canceling the caller's token.
        }
        finally
        {
            await cleanupAsync().ConfigureAwait(false);
        }
    }

    private async Task RunRuntimeScriptOnlyAsync(
        MacroSequence macro,
        PlaybackOptions options,
        double normalizedSpeed,
        int repeatCount,
        bool infiniteLoop,
        CancellationToken cancellationToken)
    {
        logLoopSettings(options, repeatCount, infiniteLoop);
        await setupRuntimeScriptOnlyAsync(macro, cancellationToken).ConfigureAwait(false);

        var iteration = 0;
        while ((infiniteLoop || iteration < repeatCount) && !cancellationToken.IsCancellationRequested)
        {
            setLoopProgress(infiniteLoop ? 0 : repeatCount, iteration + 1);
            await playOnceRuntimeScriptAsync(macro, normalizedSpeed, options, cancellationToken).ConfigureAwait(false);
            await WaitForNextIterationAsync(macro, options, normalizedSpeed, iteration, repeatCount, infiniteLoop, cancellationToken).ConfigureAwait(false);
            iteration++;
        }
    }

    private async Task WaitForNextIterationAsync(
        MacroSequence macro,
        PlaybackOptions options,
        double normalizedSpeed,
        int iteration,
        int repeatCount,
        bool infiniteLoop,
        CancellationToken cancellationToken)
    {
        long trailingDelaySource = resolveTrailingDelayMicroseconds(macro);
        if (trailingDelaySource > 0 && !cancellationToken.IsCancellationRequested)
        {
            double trailingDelay = trailingDelaySource / (double)MacroTiming.MicrosecondsPerMillisecond / normalizedSpeed;
            if (trailingDelay > 0)
            {
                await waitAsync(trailingDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        bool hasNextIteration = infiniteLoop || iteration < repeatCount - 1;
        if (!hasNextIteration || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        int delayMs = resolveRepeatDelayMs(options);
        if (delayMs > 0)
        {
            setWaitingBetweenLoops(true);
            await waitAsync(delayMs, cancellationToken).ConfigureAwait(false);
            setWaitingBetweenLoops(false);
        }
        else if ((iteration + 1) % IterationYieldInterval is 0)
        {
            await Task.Yield();
        }
    }
}
