using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Cli.Services;

public interface IWindowCliService
{
    Task<CliCommandExecutionResult> ExecuteAsync(WindowCliOptions options, CancellationToken cancellationToken);
}
