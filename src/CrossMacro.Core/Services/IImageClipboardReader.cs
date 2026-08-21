namespace CrossMacro.Core.Services;

/// <summary>
/// Reads a PNG clipboard representation when the current platform can expose it
/// without a desktop framework dependency.
/// </summary>
public interface IImageClipboardReader
{
    public bool IsSupported { get; }

    /// <summary>
    /// Returns a PNG clipboard representation, or <see langword="null"/> when
    /// the clipboard does not currently expose one. Implementations must reject
    /// data larger than <paramref name="maximumBytes"/> before allocating it.
    /// </summary>
    public Task<byte[]?> GetPngAsync(int maximumBytes, CancellationToken cancellationToken = default);
}
