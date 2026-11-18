
namespace CrossMacro.Commands.Runtime;

public interface IPreflightService
{
    public Task<OperationResult> CheckAsync(PreflightTarget target, CancellationToken cancellationToken);
}
