
namespace CrossMacro.Cli.Services;

public interface IClipboardCliService
{
    Task<CliCommandExecutionResult> GetAsync(CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> SetTextAsync(string text, CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> SetFileAsync(string filePath, CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> ClearAsync(CancellationToken cancellationToken);
}
