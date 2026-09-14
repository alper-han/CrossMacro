
namespace CrossMacro.Application.Execution;

public interface IRunScriptExecutionService
{
    public Task<MacroExecutionResult> ExecuteAsync(RunScriptExecutionRequest request, CancellationToken cancellationToken);
}
