using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class MacroValidateCommandHandler : CliCommandHandlerBase<MacroValidateCliOptions>
{
    private readonly IMacroExecutionService _macroExecutionService;

    public MacroValidateCommandHandler(IMacroExecutionService macroExecutionService)
    {
        _macroExecutionService = macroExecutionService;
    }

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(
        MacroValidateCliOptions options,
        CancellationToken cancellationToken)
    {
        var result = await _macroExecutionService.ValidateAsync(options.MacroFilePath, cancellationToken);

        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
    }
}
