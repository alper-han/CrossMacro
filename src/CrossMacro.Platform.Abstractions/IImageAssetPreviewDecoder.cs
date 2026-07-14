namespace CrossMacro.Platform.Abstractions;

public interface IImageAssetPreviewDecoder
{
    ImageAssetPreview Decode(string encoded, string? assetName = null);
}
