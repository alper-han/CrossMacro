
namespace CrossMacro.Cli.Commands;

public sealed class MacroValidateCommandHandler(IMacroExecutionService macroExecutionService) : CliCommandHandlerBase<MacroValidateOptions>
{
    private readonly IMacroExecutionService _macroExecutionService = macroExecutionService;

    protected override async Task<OperationResult> ExecuteAsync(
        MacroValidateOptions options,
        CancellationToken cancellationToken)
    {
        return await _macroExecutionService.ValidateAsync(options.MacroFilePath, cancellationToken).ConfigureAwait(false);
    }
}
