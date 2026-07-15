
namespace CrossMacro.Cli.Services;

public interface ICliPreflightService
{
    public Task<CliPreflightResult> CheckAsync(CliPreflightTarget target, CancellationToken cancellationToken);
}
