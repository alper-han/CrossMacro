namespace CrossMacro.Mcp.Tests;

internal sealed class TestImageAssetCodec : IImageAssetCodec
    {
        public byte[]? PngBytes { get; init; }

        public ScreenFrame? Frame { get; init; }

        public TestImageAssetFailure Failure { get; init; }

        public int ReadCallCount { get; private set; }

        public Task<byte[]> ReadFileAsync(string filePath, string? assetName = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCallCount++;
            return Failure switch
            {
                TestImageAssetFailure.Validation => Task.FromException<byte[]>(new InvalidDataException("invalid png")),
                TestImageAssetFailure.File => Task.FromException<byte[]>(new IOException("file read failed")),
                TestImageAssetFailure.None => Task.FromResult(PngBytes ?? throw new InvalidOperationException("PNG bytes were not configured.")),
                _ => throw new ArgumentException("Image asset failure is invalid.", nameof(filePath)),
            };
        }

        public Task<ScreenFrame> DecodeFileAsync(string filePath, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ScreenFrame DecodePng(ReadOnlySpan<byte> pngBytes, string? assetName = null) =>
            Frame ?? throw new InvalidOperationException("Image frame was not configured.");

        public Task<ScreenFrame> DecodePngAsync(ReadOnlyMemory<byte> pngBytes, string? assetName = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Frame ?? throw new InvalidOperationException("Image frame was not configured."));
        }

        public ScreenFrame DecodeBase64Png(string encoded, string? assetName = null) => throw new NotSupportedException();

        public Task<ScreenFrame> DecodeBase64PngAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void ValidateBase64Png(string encoded, string? assetName = null) => throw new NotSupportedException();

        public Task ValidateBase64PngAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void ValidateMacroBudget(long totalEncodedBytes) => throw new NotSupportedException();

        public void EncodePng(ScreenFrame frame, Stream output) => throw new NotSupportedException();

        public Task EncodePngAsync(ScreenFrame frame, Stream output, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
