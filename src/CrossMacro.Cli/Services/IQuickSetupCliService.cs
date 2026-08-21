namespace CrossMacro.Cli.Services;

public interface IQuickSetupCliService
{
    public QuickSetupStatus GetStatus();

    public Task<QuickSetupCliResult> RunAsync(CancellationToken cancellationToken);
}
