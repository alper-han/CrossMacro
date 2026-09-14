namespace CrossMacro.Cli.Services.QuickSetup;

public interface IQuickSetupCliService
{
    public QuickSetupStatus GetStatus();

    public Task<QuickSetupCliResult> RunAsync(CancellationToken cancellationToken);
}
