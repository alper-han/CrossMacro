
namespace CrossMacro.Cli.Services;

public interface IRunScriptExecutionService
{
    public Task<MacroExecutionResult> ExecuteAsync(RunCliExecutionRequest request, CancellationToken cancellationToken);
}
