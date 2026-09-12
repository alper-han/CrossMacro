namespace CrossMacro.Mcp.Tests;

internal sealed class TestImageClipboardReader : IImageClipboardReader
    {
        public bool IsSupported { get; init; } = true;

        public byte[]? PngBytes { get; init; }

        public Exception? Exception { get; init; }

        public int CallCount { get; private set; }

        public int? LastMaximumBytes { get; private set; }

        public Task<byte[]?> GetPngAsync(int maximumBytes, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastMaximumBytes = maximumBytes;
            return Exception is null
                ? Task.FromResult(PngBytes)
                : Task.FromException<byte[]?>(Exception);
        }
    }
