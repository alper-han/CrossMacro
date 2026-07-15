
namespace CrossMacro.Cli.Services;

public interface ICliPreflightService
{
    Task<CliPreflightResult> CheckAsync(CliPreflightTarget target, CancellationToken cancellationToken);
}
