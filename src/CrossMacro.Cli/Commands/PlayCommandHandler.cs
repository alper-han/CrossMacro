
namespace CrossMacro.Cli.Commands;

public sealed class PlayCommandHandler(IMacroExecutionService macroExecutionService, ICliPreflightService cliPreflightService) : CliCommandHandlerBase<PlayCliOptions>
{
    private readonly IMacroExecutionService _macroExecutionService = macroExecutionService;
    private readonly ICliPreflightService _cliPreflightService = cliPreflightService;

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(PlayCliOptions options, CancellationToken cancellationToken)
    {
        if (!options.DryRun)
        {
            var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Play, cancellationToken).ConfigureAwait(false);
            if (!preflight.Success)
            {
                return CliCommandExecutionResult.Fail(preflight.ExitCode, preflight.Message, preflight.Errors, preflight.Warnings);
            }
        }

        return await CommandTimeoutRunner.RunAsync(
            options.TimeoutSeconds,
            token => ExecuteInternalAsync(options, token),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<CliCommandExecutionResult> ExecuteInternalAsync(PlayCliOptions options, CancellationToken cancellationToken)
    {
        var effectiveLoop = options.Loop || options.RepeatCount is not 1;

        var request = new MacroExecutionRequest
        {
            MacroFilePath = options.MacroFilePath,
            SpeedMultiplier = options.SpeedMultiplier,
            Loop = effectiveLoop,
            RepeatCount = options.RepeatCount,
            RepeatDelayMs = options.RepeatDelayMs,
            MotionMode = options.MotionMode,
            StrictSpeedMotionEventsPerSecond = options.StrictSpeedMotionEventsPerSecond,
            PrecisionMotionEventsPerSecond = options.PrecisionMotionEventsPerSecond,
            MaximumMotionErrorPixels = options.MaximumMotionErrorPixels,
            CountdownSeconds = options.CountdownSeconds,
            DryRun = options.DryRun,
        };

        var result = await _macroExecutionService.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
    }
}
