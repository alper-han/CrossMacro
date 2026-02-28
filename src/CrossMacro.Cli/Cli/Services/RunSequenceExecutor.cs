using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

internal sealed class RunSequenceExecutor
{
    private readonly Func<IMacroPlayer> _macroPlayerFactory;

    public RunSequenceExecutor(Func<IMacroPlayer> macroPlayerFactory)
    {
        _macroPlayerFactory = macroPlayerFactory;
    }

    public async Task<RunSequenceExecutionResult> ExecuteAsync(
        MacroSequence sequence,
        double speedMultiplier,
        int countdownSeconds,
        int initialDelayMs,
        CancellationToken cancellationToken)
    {
        try
        {
            if (countdownSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(countdownSeconds), cancellationToken);
            }

            if (initialDelayMs > 0)
            {
                await Task.Delay(initialDelayMs, cancellationToken);
            }

            var playbackOptions = new PlaybackOptions
            {
                SpeedMultiplier = speedMultiplier
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
            return RunSequenceExecutionResult.Succeeded();
        }
        catch (OperationCanceledException)
        {
            return RunSequenceExecutionResult.Cancelled();
        }
        catch (Exception ex)
        {
            return RunSequenceExecutionResult.Failed(ex.Message);
        }
    }
}

internal sealed class RunSequenceExecutionResult
{
    private RunSequenceExecutionResult()
    {
    }

    public bool Success { get; private init; }
    public bool IsCancelled { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static RunSequenceExecutionResult Succeeded()
    {
        return new RunSequenceExecutionResult
        {
            Success = true
        };
    }

    public static RunSequenceExecutionResult Cancelled()
    {
        return new RunSequenceExecutionResult
        {
            Success = false,
            IsCancelled = true
        };
    }

    public static RunSequenceExecutionResult Failed(string errorMessage)
    {
        return new RunSequenceExecutionResult
        {
            Success = false,
            IsCancelled = false,
            ErrorMessage = errorMessage
        };
    }
}
