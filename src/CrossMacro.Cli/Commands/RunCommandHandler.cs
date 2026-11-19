namespace CrossMacro.Cli.Commands;

public sealed class RunCommandHandler(AutomationCommandService automation) : CliCommandHandlerBase<RunOptions>
{
    protected override Task<OperationResult> ExecuteAsync(RunOptions options, CancellationToken cancellationToken) =>
        automation.RunAsync(options, cancellationToken);
}
