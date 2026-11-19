namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

internal readonly record struct TemplateCacheKey(int Width, int Height, ScreenPixelFormat PixelFormat, ScreenAlphaMode AlphaMode, bool UseAlphaMask, byte AlphaThreshold, int ScaleKey, TemplateCacheContent Content);
