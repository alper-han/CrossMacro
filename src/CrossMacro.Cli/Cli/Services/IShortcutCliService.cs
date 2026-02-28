using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Cli.Services;

public interface IShortcutCliService
{
    Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> RunAsync(string taskId, CancellationToken cancellationToken);
}
