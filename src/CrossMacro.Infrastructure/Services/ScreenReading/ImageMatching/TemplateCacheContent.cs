namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

/// <summary>The raw template bytes (plus validity mask) a cached preparation was derived from.</summary>
internal sealed class TemplateCacheContent(byte[] bytes, int contentHash)
{
    public byte[] Bytes { get; } = bytes;
    public int ContentHash { get; } = contentHash;

    public static TemplateCacheContent Create(ScreenFrame template, SearchBudget budget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(template.PixelFormat);
        var rowLength = checked(template.Width * bytesPerPixel);
        var rawLength = checked((long)rowLength * template.Height);
        var validityLength = !template.IsFullyValid ? checked((long)template.Width * template.Height) : 0;
        var contentLength = checked(rawLength + validityLength);
        budget.ConsumePreparation(contentLength);
        var content = new byte[checked((int)contentLength)];
        var source = template.Pixels.Span;
        for (var y = 0; y < template.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            source.Slice(checked(y * template.Stride), rowLength).CopyTo(content.AsSpan(y * rowLength, rowLength));
        }

        if (validityLength is not 0)
        {
            var validityOffset = checked((int)rawLength);
            for (var y = 0; y < template.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < template.Width; x++)
                {
                    var point = new ScreenPoint(checked(template.LogicalBounds.X + x), checked(template.LogicalBounds.Y + y));
                    content[validityOffset + (y * template.Width) + x] = template.IsPixelValid(point) ? (byte)1 : (byte)0;
                }
            }
        }

        return new TemplateCacheContent(content, ComputeContentHash(content));
    }

    private static int ComputeContentHash(ReadOnlySpan<byte> content)
    {
        var hash = new HashCode();
        hash.AddBytes(content);
        return hash.ToHashCode();
    }
}
