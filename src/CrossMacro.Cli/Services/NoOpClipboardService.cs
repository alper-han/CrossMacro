
namespace CrossMacro.Cli.Services;

public sealed class NoOpClipboardService : IClipboardService
{
    public bool IsSupported => false;

    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}
