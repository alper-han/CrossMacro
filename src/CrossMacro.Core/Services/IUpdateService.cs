
namespace CrossMacro.Core.Services;

public interface IUpdateService
{
    public Task<UpdateCheckResult> CheckForUpdatesAsync();
}
