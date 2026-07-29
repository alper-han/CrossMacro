
namespace CrossMacro.Daemon.Services;

internal interface IPolkitAuthorizationService
{
    public Task<bool> IsInputCaptureAuthorizedAsync(uint uid, int pid, CancellationToken cancellationToken = default);
}
