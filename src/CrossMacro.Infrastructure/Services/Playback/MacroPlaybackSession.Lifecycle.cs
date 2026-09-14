namespace CrossMacro.Infrastructure.Services.Playback;

// Lifecycle and its mutable resources have one owner; no callback bundle crosses this boundary.
internal sealed partial class MacroPlaybackSession
{
    private const int IterationYieldInterval = 50;

    private async Task RunLoopAsync(MacroSequence macro, PlaybackOptions options, CancellationToken cancellationToken)
    {
        _requirements = PlaybackRequirements.Analyze(macro);
        double normalizedSpeed = PlaybackOptions.NormalizeSpeedMultiplier(options.SpeedMultiplier);
        int repeatCount = options.Loop ? options.RepeatCount : 1;
        bool infiniteLoop = options.Loop && repeatCount is 0;
        SetLoopProgress(infiniteLoop ? 0 : repeatCount, 1);

        BeginPlayback(cancellationToken);
        var sessionToken = _session.Token;

        try
        {
            if (macro.Events.Count is 0 && _requirements.HasOnlyRuntimeSteps)
            {
                await RunRuntimeScriptOnlyAsync(macro, options, normalizedSpeed, repeatCount, infiniteLoop, sessionToken).ConfigureAwait(false);
                return;
            }

            if (macro.Events.Count is 0 && !_requirements.HasRuntimeSteps)
            {
                await ExecuteScreenReadScriptStepsAsync(macro, sessionToken).ConfigureAwait(false);
                return;
            }

            await SetupPlaybackAsync(macro).ConfigureAwait(false);
            LogLoopSettings(options, repeatCount, infiniteLoop);
            await WaitForStabilizationAsync(sessionToken).ConfigureAwait(false);
            sessionToken.ThrowIfCancellationRequested();

            var iteration = 0;
            while ((infiniteLoop || iteration < repeatCount) && !sessionToken.IsCancellationRequested)
            {
                SetLoopProgress(infiniteLoop ? 0 : repeatCount, iteration + 1);
                LogIterationStart(iteration + 1);

                if (iteration > 0)
                {
                    await PrepareIterationAsync(iteration, macro, sessionToken).ConfigureAwait(false);
                }

                if (_requirements.HasRuntimeSteps)
                {
                    await PlayOnceRuntimeScriptAsync(macro, normalizedSpeed, sessionToken).ConfigureAwait(false);
                }
                else
                {
                    await PlayOnceAsync(macro, normalizedSpeed, options, sessionToken).ConfigureAwait(false);
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
            await CleanupAsync().ConfigureAwait(false);
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
        LogLoopSettings(options, repeatCount, infiniteLoop);
        await SetupRuntimeScriptOnlyAsync(macro, cancellationToken).ConfigureAwait(false);

        var iteration = 0;
        while ((infiniteLoop || iteration < repeatCount) && !cancellationToken.IsCancellationRequested)
        {
            SetLoopProgress(infiniteLoop ? 0 : repeatCount, iteration + 1);
            await PlayOnceRuntimeScriptAsync(macro, normalizedSpeed, cancellationToken).ConfigureAwait(false);
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
        long trailingDelaySource = ResolveTrailingDelayMicroseconds(macro);
        if (trailingDelaySource > 0 && !cancellationToken.IsCancellationRequested)
        {
            double trailingDelay = trailingDelaySource / (double)MacroTiming.MicrosecondsPerMillisecond / normalizedSpeed;
            if (trailingDelay > 0)
            {
                await _timingService.WaitAsync(trailingDelay, this, cancellationToken).ConfigureAwait(false);
            }
        }

        bool hasNextIteration = infiniteLoop || iteration < repeatCount - 1;
        if (!hasNextIteration || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        int delayMs = ResolveRepeatDelayMs(options);
        if (delayMs > 0)
        {
            IsWaitingBetweenLoops = true;
            await _timingService.WaitAsync(delayMs, this, cancellationToken).ConfigureAwait(false);
            IsWaitingBetweenLoops = false;
        }
        else if ((iteration + 1) % IterationYieldInterval is 0)
        {
            await Task.Yield();
        }
    }

    private static void LogIterationStart(int iteration) =>
        Log.Information("[MacroPlayer] Starting playback iteration {Iteration}", iteration);

    private static void LogLoopSettings(PlaybackOptions options, int repeatCount, bool infiniteLoop) =>
        Log.Information("[MacroPlayer] Loop settings: Loop={Loop}, RepeatCount={Count}, Infinite={Infinite}",
            options.Loop, repeatCount, infiniteLoop);

}
