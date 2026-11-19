namespace CrossMacro.Cli.Commands;

public sealed class PlayCommandHandler(AutomationCommandService automation) : CliCommandHandlerBase<PlayOptions>
{
    protected override Task<OperationResult> ExecuteAsync(PlayOptions options, CancellationToken cancellationToken) =>
        automation.PlayAsync(options, cancellationToken);
}
