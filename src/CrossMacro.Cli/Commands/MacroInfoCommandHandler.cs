
namespace CrossMacro.Cli.Commands;

public sealed class MacroInfoCommandHandler(IMacroExecutionService macroExecutionService) : CliCommandHandlerBase<MacroInfoOptions>
{
    private readonly IMacroExecutionService _macroExecutionService = macroExecutionService;

    protected override async Task<OperationResult> ExecuteAsync(
        MacroInfoOptions options,
        CancellationToken cancellationToken)
    {
        return await _macroExecutionService.GetInfoAsync(options.MacroFilePath, cancellationToken).ConfigureAwait(false);
    }
}
