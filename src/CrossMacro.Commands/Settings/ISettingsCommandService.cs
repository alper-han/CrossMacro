
namespace CrossMacro.Commands.Settings;

public interface ISettingsCommandService
{
    public Task<OperationResult> GetAsync(string? key, CancellationToken cancellationToken);

    public Task<OperationResult> SetAsync(string key, string value, CancellationToken cancellationToken);

    public Task<OperationResult> ListKeysAsync(CancellationToken cancellationToken);

    public Task<OperationResult> ResetAsync(string key, CancellationToken cancellationToken);
}
