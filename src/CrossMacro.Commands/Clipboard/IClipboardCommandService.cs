
namespace CrossMacro.Commands.Clipboard;

public interface IClipboardCommandService
{
    public Task<OperationResult> GetAsync(CancellationToken cancellationToken);

    public Task<OperationResult> SetTextAsync(string text, CancellationToken cancellationToken);

    public Task<OperationResult> SetFileAsync(string filePath, CancellationToken cancellationToken);

    public Task<OperationResult> ClearAsync(CancellationToken cancellationToken);
}
