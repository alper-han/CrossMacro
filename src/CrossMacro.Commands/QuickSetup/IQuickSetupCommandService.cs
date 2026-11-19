namespace CrossMacro.Commands.QuickSetup;

public interface IQuickSetupCommandService
{
    public QuickSetupStatus GetStatus();

    public Task<QuickSetupCommandResult> RunAsync(CancellationToken cancellationToken);
}
