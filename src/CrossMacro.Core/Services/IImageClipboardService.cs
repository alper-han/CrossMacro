
namespace CrossMacro.Core.Services;

public interface IImageClipboardService
{
    bool IsSupported { get; }

    Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default);
}
