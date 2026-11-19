
namespace CrossMacro.Application.Execution;

public interface IMacroExecutionService
{
    public Task<OperationResult> ValidateAsync(string macroFilePath, CancellationToken cancellationToken);

    public Task<OperationResult> GetInfoAsync(string macroFilePath, CancellationToken cancellationToken);

    public Task<OperationResult> ExecuteAsync(MacroExecutionRequest request, CancellationToken cancellationToken);
}
