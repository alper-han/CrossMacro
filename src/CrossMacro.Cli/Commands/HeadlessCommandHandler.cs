
namespace CrossMacro.Cli.Commands;

public sealed class HeadlessCommandHandler(IHeadlessRuntimeService headlessRuntimeService, ICliPreflightService cliPreflightService) : CliCommandHandlerBase<HeadlessCliOptions>
{
    private readonly IHeadlessRuntimeService _headlessRuntimeService = headlessRuntimeService;
    private readonly ICliPreflightService _cliPreflightService = cliPreflightService;

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(HeadlessCliOptions options, CancellationToken cancellationToken)
    {
        var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Headless, cancellationToken).ConfigureAwait(false);
        if (!preflight.Success)
        {
            return CliCommandExecutionResult.Fail(preflight.ExitCode, preflight.Message, preflight.Errors, preflight.Warnings);
        }

        var result = await _headlessRuntimeService.RunAsync(cancellationToken).ConfigureAwait(false);

        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
    }
}
