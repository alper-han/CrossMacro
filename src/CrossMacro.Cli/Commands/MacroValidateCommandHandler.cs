
namespace CrossMacro.Cli.Commands;

public sealed class MacroValidateCommandHandler(IMacroExecutionService macroExecutionService) : CliCommandHandlerBase<MacroValidateCliOptions>
{
    private readonly IMacroExecutionService _macroExecutionService = macroExecutionService;

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(
        MacroValidateCliOptions options,
        CancellationToken cancellationToken)
    {
        var result = await _macroExecutionService.ValidateAsync(options.MacroFilePath, cancellationToken).ConfigureAwait(false);

        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
    }
}
