namespace CrossMacro.Mcp.Tests;

internal sealed class TestImageClipboardService : IImageClipboardService
    {
        public bool IsSupported { get; init; } = true;

        public int SetCallCount { get; private set; }

        public byte[]? LastPngBytes { get; private set; }

        public Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetCallCount++;
            LastPngBytes = pngBytes.ToArray();
            return Task.CompletedTask;
        }
    }
