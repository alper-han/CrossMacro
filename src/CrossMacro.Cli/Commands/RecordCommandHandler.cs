namespace CrossMacro.Cli.Commands;

public sealed class RecordCommandHandler(AutomationCommandService automation) : CliCommandHandlerBase<RecordOptions>
{
    protected override Task<OperationResult> ExecuteAsync(RecordOptions options, CancellationToken cancellationToken) =>
        automation.RecordAsync(options, cancellationToken);
}
