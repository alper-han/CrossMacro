using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

public sealed class NoOpClipboardService : IClipboardService
{
    public bool IsSupported => false;

    public Task SetTextAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}

public sealed class NoOpImageClipboardService : IImageClipboardService
{
    public bool IsSupported => false;

    public Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
