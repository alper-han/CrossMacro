
namespace CrossMacro.Cli.Services.Runtime;

public interface ICliPreflightService
{
    public Task<CliPreflightResult> CheckAsync(CliPreflightTarget target, CancellationToken cancellationToken);
}
