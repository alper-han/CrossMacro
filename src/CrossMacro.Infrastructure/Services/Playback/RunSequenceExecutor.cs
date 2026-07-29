
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class RunSequenceExecutor
{
    private readonly Func<IMacroPlayer> _macroPlayerFactory;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly Func<int, int, int> _randomInclusive;

    public RunSequenceExecutor(
        Func<IMacroPlayer> macroPlayerFactory,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
        : this(macroPlayerFactory, delayAsync, RandomNumberGeneratorUtility.GetInt32Inclusive)
    {
    }

    internal RunSequenceExecutor(
        Func<IMacroPlayer> macroPlayerFactory,
        Func<TimeSpan, CancellationToken, Task>? delayAsync,
        Func<int, int, int> randomInclusive)
    {
        _macroPlayerFactory = macroPlayerFactory ?? throw new ArgumentNullException(nameof(macroPlayerFactory));
        _delayAsync = delayAsync ?? Task.Delay;
        _randomInclusive = randomInclusive ?? throw new ArgumentNullException(nameof(randomInclusive));
    }

    public async Task<RunSequenceExecutionResult> ExecuteAsync(
        MacroSequence sequence,
        double speedMultiplier,
        int countdownSeconds,
        int initialDelayMs,
        bool initialHasRandomDelay,
        int initialRandomDelayMinMs,
        int initialRandomDelayMaxMs,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalizedSpeed = PlaybackOptions.NormalizeSpeedMultiplier(speedMultiplier);
            if (countdownSeconds > 0)
            {
                await _delayAsync(TimeSpan.FromSeconds(countdownSeconds), cancellationToken).ConfigureAwait(false);
            }

            var resolvedInitialDelayMs = ResolveDelayMs(
                initialDelayMs,
                initialHasRandomDelay,
                initialRandomDelayMinMs,
                initialRandomDelayMaxMs);
            if (resolvedInitialDelayMs > 0)
            {
                var adjustedInitialDelayMs = (int)Math.Floor(resolvedInitialDelayMs / normalizedSpeed);
                if (adjustedInitialDelayMs > 0)
                {
                    await _delayAsync(TimeSpan.FromMilliseconds(adjustedInitialDelayMs), cancellationToken).ConfigureAwait(false);
                }
            }

            var playbackOptions = new PlaybackOptions
            {
                SpeedMultiplier = normalizedSpeed,
            };

            using var player = _macroPlayerFactory();
            using var stopRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    player.StopPlayback();
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.Debug(ex, "[RunSequenceExecutor] StopPlayback callback failed during cancellation.");
                }
            });

            await player.PlayAsync(sequence, playbackOptions, cancellationToken).ConfigureAwait(false);
            var runtimeVariables = player is IRunScriptRuntimeVariableSource variableSource
                ? variableSource.RuntimeVariables
                : null;
            return RunSequenceExecutionResult.Succeeded(runtimeVariables);
        }
        catch (OperationCanceledException)
        {
            return RunSequenceExecutionResult.Cancelled();
        }
        catch (AbsolutePlaybackUnsupportedException ex)
        {
            return RunSequenceExecutionResult.AbsolutePlaybackUnsupported(ex.Message);
        }
        catch (InputInjectionPermissionRequiredException ex)
        {
            return RunSequenceExecutionResult.InputInjectionPermissionRequired(ex.Message);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return RunSequenceExecutionResult.Failed(ex.Message);
        }
    }

    private int ResolveDelayMs(int fixedDelayMs, bool hasRandomDelay, int randomDelayMinMs, int randomDelayMaxMs)
    {
        long totalDelayMs = Math.Max(0, fixedDelayMs);

        if (hasRandomDelay)
        {
            var min = Math.Min(randomDelayMinMs, randomDelayMaxMs);
            var max = Math.Max(randomDelayMinMs, randomDelayMaxMs);
            totalDelayMs += ResolveRandomDelay(min, max);
        }

        if (totalDelayMs > int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)totalDelayMs;
    }

    private int ResolveRandomDelay(int min, int max)
    {
        return min == max ? min : _randomInclusive(min, max);
    }
}
