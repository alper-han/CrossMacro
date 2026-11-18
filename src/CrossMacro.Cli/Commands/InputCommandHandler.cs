
namespace CrossMacro.Cli.Commands;

public sealed class InputCommandHandler(
    IRunScriptExecutionService runScriptExecutionService,
    IPreflightService preflightService) : CliCommandHandlerBase<InputOptions>
{
    private readonly IRunScriptExecutionService _runScriptExecutionService = runScriptExecutionService ?? throw new ArgumentNullException(nameof(runScriptExecutionService));
    private readonly IPreflightService _preflightService = preflightService ?? throw new ArgumentNullException(nameof(preflightService));

    protected override async Task<OperationResult> ExecuteAsync(InputOptions options, CancellationToken cancellationToken)
    {
        if (!options.DryRun)
        {
            var preflight = await _preflightService.CheckAsync(PreflightTarget.Run, cancellationToken).ConfigureAwait(false);
            if (!preflight.Success)
            {
                return preflight;
            }
        }

        return await _runScriptExecutionService.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps = [options.Step],
            DryRun = options.DryRun,
        }, cancellationToken).ConfigureAwait(false);
    }
}
