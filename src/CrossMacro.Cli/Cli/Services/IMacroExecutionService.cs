using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Cli.Services;

public interface IMacroExecutionService
{
    Task<MacroExecutionResult> ValidateAsync(string macroFilePath, CancellationToken cancellationToken);

    Task<MacroExecutionResult> GetInfoAsync(string macroFilePath, CancellationToken cancellationToken);

    Task<MacroExecutionResult> ExecuteAsync(MacroExecutionRequest request, CancellationToken cancellationToken);
}
