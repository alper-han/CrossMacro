using System.Threading.Tasks;
using CrossMacro.Daemon.Security;

namespace CrossMacro.Daemon.Services;

public sealed class PolkitAuthorizationService : IPolkitAuthorizationService
{
    public Task<bool> IsInputCaptureAuthorizedAsync(uint uid, int pid) => PolkitChecker.CheckAuthorizationAsync(uid, pid, PolkitChecker.Actions.InputCapture);
}
