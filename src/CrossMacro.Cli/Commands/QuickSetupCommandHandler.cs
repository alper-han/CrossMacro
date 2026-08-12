namespace CrossMacro.Cli.Commands;

public sealed class QuickSetupCommandHandler(IQuickSetupCliService quickSetupService)
    : CliCommandHandlerBase<QuickSetupCliOptions>
{
    private readonly IQuickSetupCliService _quickSetupService = quickSetupService;

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(
        QuickSetupCliOptions options,
        CancellationToken cancellationToken)
    {
        var setup = await _quickSetupService.RunAsync(cancellationToken).ConfigureAwait(false);
        var data = new QuickSetupCommandData(setup.Provider, setup.Applicable, setup.Result.Success);

        if (!setup.Applicable)
        {
            return CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Temporary input setup is not applicable in this session.",
                errors: [setup.Result.Message],
                data: data);
        }

        return setup.Result.Success
            ? CliCommandExecutionResult.Ok(setup.Result.Message, data)
            : CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Temporary input setup failed.",
                errors: [setup.Result.Message],
                data: data);
    }
}
