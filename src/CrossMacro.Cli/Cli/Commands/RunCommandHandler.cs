
namespace CrossMacro.Cli.Commands;

public sealed class RunCommandHandler : CliCommandHandlerBase<RunCliOptions>
{
    private readonly IRunScriptExecutionService _runScriptExecutionService;
    private readonly ICliPreflightService _cliPreflightService;

    public RunCommandHandler(IRunScriptExecutionService runScriptExecutionService, ICliPreflightService cliPreflightService)
    {
        _runScriptExecutionService = runScriptExecutionService;
        _cliPreflightService = cliPreflightService;
    }

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
            cancellationToken,
            token => ExecuteInternalAsync(options, token)).ConfigureAwait(false);
    }

    private async Task<CliCommandExecutionResult> ExecuteInternalAsync(RunCliOptions options, CancellationToken cancellationToken)
    {
        var result = await _runScriptExecutionService.ExecuteAsync(new Services.RunExecutionRequest
        {
            Steps = options.Steps,
            StepFilePath = options.StepFilePath,
            SpeedMultiplier = options.SpeedMultiplier,
            CountdownSeconds = options.CountdownSeconds,
            DryRun = options.DryRun,
        }, cancellationToken).ConfigureAwait(false);

        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
    }
}
