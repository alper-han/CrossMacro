using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Cli.Services;

public interface ISettingsCliService
{
    Task<SettingsCommandResult> GetAsync(string? key, CancellationToken cancellationToken);

    Task<SettingsCommandResult> SetAsync(string key, string value, CancellationToken cancellationToken);
}
