using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

public sealed class NoOpImageClipboardService : IImageClipboardService
{
    public bool IsSupported => false;

    public Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
