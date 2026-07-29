
namespace CrossMacro.Cli.Services;

public interface IClipboardCliService
{
    public Task<CliCommandExecutionResult> GetAsync(CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> SetTextAsync(string text, CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> SetFileAsync(string filePath, CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> ClearAsync(CancellationToken cancellationToken);
}
