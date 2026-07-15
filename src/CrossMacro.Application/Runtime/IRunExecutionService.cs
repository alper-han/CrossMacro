using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Application.Runtime;

public interface IRunExecutionService
{
    Task<RunExecutionResult> ExecuteAsync(
        RunExecutionRequest request,
        CancellationToken cancellationToken = default);
}
