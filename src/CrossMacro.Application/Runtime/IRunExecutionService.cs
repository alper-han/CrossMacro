
namespace CrossMacro.Application.Runtime;

public interface IRunExecutionService
{
    public Task<RunExecutionResult> ExecuteAsync(
        RunExecutionRequest request,
        CancellationToken cancellationToken = default);
}
