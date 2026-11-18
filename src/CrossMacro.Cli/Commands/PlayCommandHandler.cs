
namespace CrossMacro.Cli.Commands;

public sealed class PlayCommandHandler(IMacroExecutionService macroExecutionService, IPreflightService preflightService) : CliCommandHandlerBase<PlayOptions>
{
    private readonly IMacroExecutionService _macroExecutionService = macroExecutionService;
    private readonly IPreflightService _preflightService = preflightService;

    protected override async Task<OperationResult> ExecuteAsync(PlayOptions options, CancellationToken cancellationToken)
    {
        if (!options.DryRun)
        {
            var preflight = await _preflightService.CheckAsync(PreflightTarget.Play, cancellationToken).ConfigureAwait(false);
            if (!preflight.Success)
            {
                return preflight;
            }
        }

        return await CommandTimeoutRunner.RunAsync(
            options.TimeoutSeconds,
            token => ExecuteInternalAsync(options, token),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<OperationResult> ExecuteInternalAsync(PlayOptions options, CancellationToken cancellationToken)
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

        return await _macroExecutionService.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
