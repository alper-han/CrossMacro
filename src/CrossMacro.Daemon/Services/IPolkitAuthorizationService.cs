
namespace CrossMacro.Daemon.Services;

public interface IPolkitAuthorizationService
{
    public Task<bool> IsInputCaptureAuthorizedAsync(uint uid, int pid);
}
