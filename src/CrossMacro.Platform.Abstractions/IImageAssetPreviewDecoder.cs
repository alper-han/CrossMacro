namespace CrossMacro.Platform.Abstractions;

public interface IImageAssetPreviewDecoder
{
    public ImageAssetPreview Decode(string encoded, string? assetName = null);

    public Task<ImageAssetPreview> DecodeAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default);
}
