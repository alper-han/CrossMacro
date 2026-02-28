using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class HeadlessCommandHandler : CliCommandHandlerBase<HeadlessCliOptions>
{
    private readonly IHeadlessRuntimeService _headlessRuntimeService;
    private readonly ICliPreflightService _cliPreflightService;

    public HeadlessCommandHandler(IHeadlessRuntimeService headlessRuntimeService, ICliPreflightService cliPreflightService)
    {
        _headlessRuntimeService = headlessRuntimeService;
        _cliPreflightService = cliPreflightService;
    }

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(HeadlessCliOptions options, CancellationToken cancellationToken)
    {
        var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Headless, cancellationToken);
        if (!preflight.Success)
        {
            return CliCommandExecutionResult.Fail(preflight.ExitCode, preflight.Message, preflight.Errors, preflight.Warnings);
        }

        var result = await _headlessRuntimeService.RunAsync(cancellationToken);

        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
    }
}
