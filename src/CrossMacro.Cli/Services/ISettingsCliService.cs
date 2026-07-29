
namespace CrossMacro.Cli.Services;

public interface ISettingsCliService
{
    public Task<SettingsCommandResult> GetAsync(string? key, CancellationToken cancellationToken);

    public Task<SettingsCommandResult> SetAsync(string key, string value, CancellationToken cancellationToken);

    public Task<SettingsCommandResult> ListKeysAsync(CancellationToken cancellationToken);

    public Task<SettingsCommandResult> ResetAsync(string key, CancellationToken cancellationToken);
}
