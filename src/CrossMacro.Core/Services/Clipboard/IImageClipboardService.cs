
namespace CrossMacro.Core.Services.Clipboard;

public interface IImageClipboardService
{
    public bool IsSupported { get; }

    public Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default);
}
