using Avalonia.Media.Imaging;

namespace CrossMacro.UI.Services;

public interface IImageAssetPreviewDecoder
{
    WriteableBitmap Decode(string encoded, string? assetName = null);
}
