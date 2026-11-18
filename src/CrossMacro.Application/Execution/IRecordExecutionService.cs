
namespace CrossMacro.Application.Execution;

public interface IRecordExecutionService
{
    public Task<OperationResult> ExecuteAsync(RecordExecutionRequest request, CancellationToken cancellationToken);
}
