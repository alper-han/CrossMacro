
namespace CrossMacro.Cli.Commands;

public sealed class RunCommandHandler(IRunScriptExecutionService runScriptExecutionService, ICliPreflightService cliPreflightService) : CliCommandHandlerBase<RunCliOptions>
{
    private readonly IRunScriptExecutionService _runScriptExecutionService = runScriptExecutionService;
    private readonly ICliPreflightService _cliPreflightService = cliPreflightService;

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(RunCliOptions options, CancellationToken cancellationToken)
    {
        if (!options.DryRun)
        {
            var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Run, cancellationToken).ConfigureAwait(false);
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

    private async Task<CliCommandExecutionResult> ExecuteInternalAsync(RunCliOptions options, CancellationToken cancellationToken)
    {
        var result = await _runScriptExecutionService.ExecuteAsync(new Services.RunCliExecutionRequest
        {
            Steps = options.Steps,
            StepFilePath = options.StepFilePath,
            SpeedMultiplier = options.SpeedMultiplier,
            CountdownSeconds = options.CountdownSeconds,
            DryRun = options.DryRun,
            ImageAssets = options.ImageAssets ?? [],
        }, cancellationToken).ConfigureAwait(false);

        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
    }
}
