namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

internal sealed class TemplateCacheKeyComparer : IEqualityComparer<TemplateCacheKey>
{
    public static TemplateCacheKeyComparer Instance { get; } = new();

    public bool Equals(TemplateCacheKey left, TemplateCacheKey right)
    {
        return left.Width == right.Width && left.Height == right.Height && left.PixelFormat == right.PixelFormat && left.AlphaMode == right.AlphaMode && left.UseAlphaMask == right.UseAlphaMask && left.AlphaThreshold == right.AlphaThreshold && left.ScaleKey == right.ScaleKey && left.Content.ContentHash == right.Content.ContentHash && left.Content.Bytes.AsSpan().SequenceEqual(right.Content.Bytes);
    }

    public int GetHashCode(TemplateCacheKey key)
    {
        var hash = new HashCode();
        hash.Add(key.Width);
        hash.Add(key.Height);
        hash.Add(key.PixelFormat);
        hash.Add(key.AlphaMode);
        hash.Add(key.UseAlphaMask);
        hash.Add(key.AlphaThreshold);
        hash.Add(key.ScaleKey);
        hash.Add(key.Content.ContentHash);
        return hash.ToHashCode();
    }
}
