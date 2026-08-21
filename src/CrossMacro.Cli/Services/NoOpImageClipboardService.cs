
namespace CrossMacro.Cli.Services;

public sealed class NoOpImageClipboardService : IImageClipboardService, IImageClipboardReader
{
    public bool IsSupported => false;

    public Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<byte[]?> GetPngAsync(int maximumBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new ImageClipboardUnavailableException("PNG image clipboard reading is not supported in this runtime.");
    }
}
