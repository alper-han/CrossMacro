
namespace CrossMacro.Core.Services.Updates;

public interface IUpdateService
{
    public Task<UpdateCheckResult> CheckForUpdatesAsync();
}
