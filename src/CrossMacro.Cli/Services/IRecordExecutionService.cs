
namespace CrossMacro.Cli.Services;

public interface IRecordExecutionService
{
    public Task<RecordExecutionResult> ExecuteAsync(RecordExecutionRequest request, CancellationToken cancellationToken);
}
