namespace CrossMacro.Platform.Abstractions;

public interface IImageAssetCodec
{
    public Task<ScreenFrame> DecodeFileAsync(string filePath, CancellationToken cancellationToken = default);

    public ScreenFrame DecodePng(ReadOnlySpan<byte> pngBytes, string? assetName = null);

    public Task<ScreenFrame> DecodePngAsync(ReadOnlyMemory<byte> pngBytes, string? assetName = null, CancellationToken cancellationToken = default);

    public ScreenFrame DecodeBase64Png(string encoded, string? assetName = null);

    public Task<ScreenFrame> DecodeBase64PngAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default);

    public void ValidateBase64Png(string encoded, string? assetName = null);

    public Task ValidateBase64PngAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default);

    public void ValidateMacroBudget(long totalEncodedBytes);

    public void EncodePng(ScreenFrame frame, Stream output);

    public Task EncodePngAsync(ScreenFrame frame, Stream output, CancellationToken cancellationToken = default);
}
