
namespace CrossMacro.Cli.Commands;

public sealed class RunCommandHandler(IRunScriptExecutionService runScriptExecutionService, IPreflightService preflightService) : CliCommandHandlerBase<RunOptions>
{
    private readonly IRunScriptExecutionService _runScriptExecutionService = runScriptExecutionService;
    private readonly IPreflightService _preflightService = preflightService;

    protected override async Task<OperationResult> ExecuteAsync(RunOptions options, CancellationToken cancellationToken)
    {
        if (!options.DryRun)
        {
            var preflight = await _preflightService.CheckAsync(PreflightTarget.Run, cancellationToken).ConfigureAwait(false);
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

    private async Task<OperationResult> ExecuteInternalAsync(RunOptions options, CancellationToken cancellationToken)
    {
        return await _runScriptExecutionService.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps = options.Steps,
            StepFilePath = options.StepFilePath,
            SpeedMultiplier = options.SpeedMultiplier,
            CountdownSeconds = options.CountdownSeconds,
            DryRun = options.DryRun,
            ImageAssets = options.ImageAssets ?? [],
        }, cancellationToken).ConfigureAwait(false);
    }
}
