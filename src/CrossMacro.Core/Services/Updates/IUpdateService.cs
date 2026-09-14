
namespace CrossMacro.Core.Services.Updates;

public interface IUpdateService
{
    public Task<UpdateCheckResult> CheckForUpdatesAsync();

    public Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return CheckForUpdatesAsync();
    }
}
