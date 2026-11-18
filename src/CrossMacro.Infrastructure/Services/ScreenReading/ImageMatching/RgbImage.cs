namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

/// <summary>Tightly packed RGB pixels with an optional coverage mask; zero coverage excludes a pixel from matching.</summary>
internal readonly record struct RgbImage(int Width, int Height, byte[] Pixels, int RowStride, byte[]? AlphaMask = null, int EffectivePixelCount = -1);
