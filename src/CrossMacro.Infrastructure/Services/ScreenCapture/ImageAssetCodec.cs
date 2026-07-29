
namespace CrossMacro.Infrastructure.Services.ScreenCapture;

public sealed class ImageAssetCodec : IImageAssetCodec
{
    public async Task<ScreenFrame> DecodeFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ScreenImageAssetPolicy.ValidateFileLength(filePath);
        var pngBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await DecodePngAsync(pngBytes, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public ScreenFrame DecodePng(ReadOnlySpan<byte> pngBytes, string? assetName = null) =>
        ScreenImageAssetPolicy.DecodePng(pngBytes, assetName);

    public Task<ScreenFrame> DecodePngAsync(ReadOnlyMemory<byte> pngBytes, string? assetName = null, CancellationToken cancellationToken = default) =>
        ScreenImageAssetPolicy.DecodePngAsync(pngBytes, assetName, cancellationToken);

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

    public async Task<ScreenFrame> DecodeBase64PngAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default)
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

        return await DecodePngAsync(pngBytes, assetName, cancellationToken).ConfigureAwait(false);
    }

    public async Task ValidateBase64PngAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default)
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

        var validation = await ScreenImageAssetPolicy.TryValidateEncodedPngAsync(pngBytes, assetName, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(validation.Error ?? $"Image asset '{assetName}' is not a supported PNG.");
        }
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

    public Task EncodePngAsync(ScreenFrame frame, Stream output, CancellationToken cancellationToken = default) =>
        ScreenFramePngEncoder.EncodeAsync(frame, output, cancellationToken);
}
