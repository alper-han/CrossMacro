namespace CrossMacro.Cli.Services;

public interface IQuickSetupCliService
{
    public Task<QuickSetupCliResult> RunAsync(CancellationToken cancellationToken);
}
