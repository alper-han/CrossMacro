
namespace CrossMacro.Infrastructure.Services.ScreenCapture;

public sealed class ImageAssetCodec : IImageAssetCodec
{
    public async Task<ScreenFrame> DecodeFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ScreenImageAssetPolicy.ValidateFileLength(filePath);
        var pngBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        return DecodePng(pngBytes);
    }

    public ScreenFrame DecodePng(ReadOnlySpan<byte> pngBytes, string? assetName = null) =>
        ScreenImageAssetPolicy.DecodePng(pngBytes, assetName);

    public ScreenFrame DecodeBase64Png(string encoded, string? assetName = null)
    {
        ScreenImageAssetPolicy.ValidateBase64Length(encoded, assetName);
        byte[] pngBytes;
        try
        {
            pngBytes = Convert.FromBase64String(encoded.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException($"Image asset '{assetName}': Image asset is not valid Base64.", ex);
        }

        return DecodePng(pngBytes, assetName);
    }

    public void ValidateBase64Png(string encoded, string? assetName = null)
    {
        ScreenImageAssetPolicy.ValidateBase64Length(encoded, assetName);
        byte[] pngBytes;
        try
        {
            pngBytes = Convert.FromBase64String(encoded.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException($"Image asset '{assetName}': Image asset is not valid Base64.", ex);
        }

        ScreenImageAssetPolicy.ValidatePng(pngBytes, assetName);
    }

    public void ValidateMacroBudget(long totalEncodedBytes) =>
        ScreenImageAssetPolicy.ValidateMacroBudget(totalEncodedBytes);

    public void EncodePng(ScreenFrame frame, Stream output) => ScreenFramePngEncoder.Encode(frame, output);
}
