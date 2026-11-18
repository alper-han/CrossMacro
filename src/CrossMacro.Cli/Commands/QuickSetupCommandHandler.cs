using CrossMacro.Commands.QuickSetup;

namespace CrossMacro.Cli.Commands;

public sealed class QuickSetupCommandHandler(IQuickSetupCommandService quickSetupService)
    : CliCommandHandlerBase<QuickSetupOptions>
{
    private readonly IQuickSetupCommandService _quickSetupService = quickSetupService;

    protected override async Task<OperationResult> ExecuteAsync(
        QuickSetupOptions options,
        CancellationToken cancellationToken)
    {
        var setup = await _quickSetupService.RunAsync(cancellationToken).ConfigureAwait(false);
        var data = new QuickSetupCommandData(setup.Provider, setup.Applicable, setup.Result.Success);

        if (!setup.Applicable)
        {
            return OperationResult.Fail(
                ExecutionOutcomeCode.EnvironmentError,
                "Temporary input setup is not applicable in this session.",
                errors: [setup.Result.Message],
                data: data);
        }

        return setup.Result.Success
            ? OperationResult.Ok(setup.Result.Message, data)
            : OperationResult.Fail(
                ExecutionOutcomeCode.EnvironmentError,
                "Temporary input setup failed.",
                errors: [setup.Result.Message],
                data: data);
    }
}
