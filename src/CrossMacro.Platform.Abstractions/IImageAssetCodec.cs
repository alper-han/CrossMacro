namespace CrossMacro.Platform.Abstractions;

public interface IImageAssetCodec
{
    Task<ScreenFrame> DecodeFileAsync(string filePath, CancellationToken cancellationToken = default);

    ScreenFrame DecodePng(ReadOnlySpan<byte> pngBytes, string? assetName = null);

    ScreenFrame DecodeBase64Png(string encoded, string? assetName = null);

    void ValidateBase64Png(string encoded, string? assetName = null);

    void ValidateMacroBudget(long totalEncodedBytes);

    void EncodePng(ScreenFrame frame, Stream output);
}
