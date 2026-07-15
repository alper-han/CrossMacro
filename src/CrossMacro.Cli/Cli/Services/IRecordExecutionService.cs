
namespace CrossMacro.Cli.Services;

public interface IRecordExecutionService
{
    Task<RecordExecutionResult> ExecuteAsync(RecordExecutionRequest request, CancellationToken cancellationToken);
}
