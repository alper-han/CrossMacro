using System.Threading.Tasks;

namespace CrossMacro.Daemon.Services;

public interface IPolkitAuthorizationService
{
    Task<bool> IsInputCaptureAuthorizedAsync(uint uid, int pid);
}
