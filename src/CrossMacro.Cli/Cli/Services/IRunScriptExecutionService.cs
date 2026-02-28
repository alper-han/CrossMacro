using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Cli.Services;

public interface IRunScriptExecutionService
{
    Task<MacroExecutionResult> ExecuteAsync(RunExecutionRequest request, CancellationToken cancellationToken);
}
