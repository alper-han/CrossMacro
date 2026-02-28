using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Cli;

public interface ICliCommandHandler
{
    bool CanHandle(CliCommandOptions options);

    Task<CliCommandExecutionResult> ExecuteAsync(CliCommandOptions options, CancellationToken cancellationToken);
}
