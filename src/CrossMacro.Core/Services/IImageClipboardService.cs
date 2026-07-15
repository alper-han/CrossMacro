
namespace CrossMacro.Core.Services;

public interface IImageClipboardService
{
    public bool IsSupported { get; }

    public Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default);
}
