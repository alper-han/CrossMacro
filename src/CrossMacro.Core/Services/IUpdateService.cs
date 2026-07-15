
namespace CrossMacro.Core.Services;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync();
}
