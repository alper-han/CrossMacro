
namespace CrossMacro.Daemon.Services;

internal interface IPolkitAuthorizationService
{
    public Task<bool> IsAuthorizedAsync(uint uid, int pid, CancellationToken cancellationToken = default);
}
