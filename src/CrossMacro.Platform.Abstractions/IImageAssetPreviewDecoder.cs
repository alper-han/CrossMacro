namespace CrossMacro.Platform.Abstractions;

public interface IImageAssetPreviewDecoder
{
    public ImageAssetPreview Decode(string encoded, string? assetName = null);
}
