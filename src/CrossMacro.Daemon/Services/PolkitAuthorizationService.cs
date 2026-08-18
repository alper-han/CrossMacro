
namespace CrossMacro.Daemon.Services;

internal sealed class PolkitAuthorizationService : IPolkitAuthorizationService
{
    public Task<bool> IsInputCaptureAuthorizedAsync(uint uid, int pid, CancellationToken cancellationToken = default) =>
        PolkitChecker.CheckAuthorizationAsync(uid, pid, PolkitChecker.Actions.InputCapture, cancellationToken);
}
