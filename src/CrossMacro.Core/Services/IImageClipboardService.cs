using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Core.Services;

public interface IImageClipboardService
{
    bool IsSupported { get; }

    Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default);
}
