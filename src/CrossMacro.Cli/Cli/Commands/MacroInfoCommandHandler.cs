using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class MacroInfoCommandHandler : CliCommandHandlerBase<MacroInfoCliOptions>
{
    private readonly IMacroExecutionService _macroExecutionService;

    public MacroInfoCommandHandler(IMacroExecutionService macroExecutionService)
    {
        _macroExecutionService = macroExecutionService;
    }

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(
        MacroInfoCliOptions options,
        CancellationToken cancellationToken)
    {
        var result = await _macroExecutionService.GetInfoAsync(options.MacroFilePath, cancellationToken);

        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
    }
}
