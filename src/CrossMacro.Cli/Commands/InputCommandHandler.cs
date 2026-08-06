using CrossMacro.Cli.Options;

namespace CrossMacro.Cli.Commands;

public sealed class InputCommandHandler(
    IRunScriptExecutionService runScriptExecutionService,
    ICliPreflightService cliPreflightService) : CliCommandHandlerBase<InputCliOptions>
{
    private readonly IRunScriptExecutionService _runScriptExecutionService = runScriptExecutionService ?? throw new ArgumentNullException(nameof(runScriptExecutionService));
    private readonly ICliPreflightService _cliPreflightService = cliPreflightService ?? throw new ArgumentNullException(nameof(cliPreflightService));

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(InputCliOptions options, CancellationToken cancellationToken)
    {
        if (!options.DryRun)
        {
            var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Run, cancellationToken).ConfigureAwait(false);
            if (!preflight.Success)
            {
                return CliCommandExecutionResult.Fail(preflight.ExitCode, preflight.Message, preflight.Errors, preflight.Warnings);
            }
        }

        var result = await _runScriptExecutionService.ExecuteAsync(new Services.RunCliExecutionRequest
        {
            Steps = [options.Step],
            DryRun = options.DryRun,
        }, cancellationToken).ConfigureAwait(false);

        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
    }
}
