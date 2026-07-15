
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class RunSequenceExecutor
{
    private readonly Func<IMacroPlayer> _macroPlayerFactory;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    public RunSequenceExecutor(
        Func<IMacroPlayer> macroPlayerFactory,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _macroPlayerFactory = macroPlayerFactory ?? throw new ArgumentNullException(nameof(macroPlayerFactory));
        _delayAsync = delayAsync ?? Task.Delay;
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
                await _delayAsync(TimeSpan.FromSeconds(countdownSeconds), cancellationToken);
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
                    await _delayAsync(TimeSpan.FromMilliseconds(adjustedInitialDelayMs), cancellationToken);
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
                    player.Stop();
                }
                catch
                {
                }
            });

            await player.PlayAsync(sequence, playbackOptions, cancellationToken);
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
        catch (Exception ex)
        {
            return RunSequenceExecutionResult.Failed(ex.Message);
        }
    }

    private static int ResolveDelayMs(int fixedDelayMs, bool hasRandomDelay, int randomDelayMinMs, int randomDelayMaxMs)
    {
        long totalDelayMs = Math.Max(0, fixedDelayMs);

        if (hasRandomDelay)
        {
            var min = Math.Min(randomDelayMinMs, randomDelayMaxMs);
            var max = Math.Max(randomDelayMinMs, randomDelayMaxMs);
            if (min == max)
            {
                totalDelayMs += min;
            }
            else if (max == int.MaxValue)
            {
                totalDelayMs += Random.Shared.NextInt64(min, (long)max + 1);
            }
            else
            {
                totalDelayMs += Random.Shared.Next(min, max + 1);
            }
        }

        if (totalDelayMs > int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)totalDelayMs;
    }
}
