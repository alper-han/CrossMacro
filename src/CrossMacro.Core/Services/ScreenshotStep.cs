namespace CrossMacro.Core.Services;

public readonly record struct ScreenshotStep(
    string? OutputPath,
    bool CopyToClipboard,
    bool UseRegion,
    string RegionX,
    string RegionY,
    string RegionWidth,
    string RegionHeight);
