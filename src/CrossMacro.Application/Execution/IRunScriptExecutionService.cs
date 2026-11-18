
namespace CrossMacro.Application.Execution;

public interface IRunScriptExecutionService
{
    public Task<OperationResult> ExecuteAsync(RunScriptExecutionRequest request, CancellationToken cancellationToken);
}
