using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Cli.Services;

public interface IScreenshotCliService
{
    Task<CliCommandExecutionResult> ExecuteAsync(ScreenshotCliOptions options, CancellationToken cancellationToken);
}
