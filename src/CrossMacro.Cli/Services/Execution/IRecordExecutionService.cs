
namespace CrossMacro.Cli.Services.Execution;

public interface IRecordExecutionService
{
    public Task<RecordExecutionResult> ExecuteAsync(RecordExecutionRequest request, CancellationToken cancellationToken);
}
