
namespace CrossMacro.Cli.Services;

public interface IRunScriptExecutionService
{
    public Task<MacroExecutionResult> ExecuteAsync(RunExecutionRequest request, CancellationToken cancellationToken);
}
