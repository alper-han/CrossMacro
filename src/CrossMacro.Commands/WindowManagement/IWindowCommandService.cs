
namespace CrossMacro.Commands.WindowManagement;

public interface IWindowCommandService
{
    public Task<OperationResult> ExecuteAsync(WindowOptions options, CancellationToken cancellationToken);
}
