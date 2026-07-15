namespace CrossMacro.Platform.Abstractions;

public interface IImageAssetCodec
{
    public Task<ScreenFrame> DecodeFileAsync(string filePath, CancellationToken cancellationToken = default);

    public ScreenFrame DecodePng(ReadOnlySpan<byte> pngBytes, string? assetName = null);

    public ScreenFrame DecodeBase64Png(string encoded, string? assetName = null);

    public void ValidateBase64Png(string encoded, string? assetName = null);

    public void ValidateMacroBudget(long totalEncodedBytes);

    public void EncodePng(ScreenFrame frame, Stream output);
}
