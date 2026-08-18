namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal static class WaylandLogicalPhysicalMapper
{
    public static int MapPixel(int logicalPixel, int logicalExtent, int physicalExtent)
    {
        if (logicalPixel < 0 || logicalPixel >= logicalExtent)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalPixel), logicalPixel, "Logical pixel is outside the source extent.");
        }

        if (logicalExtent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalExtent), logicalExtent, "Logical extent must be positive.");
        }

        if (physicalExtent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalExtent), physicalExtent, "Physical extent must be positive.");
        }

        var mapped = ((logicalPixel * 2L) + 1) * physicalExtent / (2L * logicalExtent);
        return (int)Math.Clamp(mapped, 0L, physicalExtent - 1L);
    }
}
