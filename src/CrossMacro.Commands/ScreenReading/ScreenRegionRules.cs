namespace CrossMacro.Commands.ScreenReading;

/// <summary>
/// The single definition of a valid screen region (positive size, end-exclusive endpoint inside the
/// 32-bit coordinate range), shared by the CLI parsers, the command services and the MCP tools.
/// </summary>
public static class ScreenRegionRules
{
    /// <returns>An error message prefixed with <paramref name="subject"/>, or null when the region is valid.</returns>
    public static string? Validate(int x, int y, long width, long height, string subject)
    {
        if (width <= 0 || height <= 0)
        {
            return $"{subject} width and height must be positive.";
        }

        if (width > int.MaxValue || height > int.MaxValue
            || x + width > int.MaxValue
            || y + height > int.MaxValue)
        {
            return $"{subject} endpoint exceeds the supported screen coordinate range.";
        }

        return null;
    }

    /// <summary>
    /// Builds an optional region from four nullable components: all null yields no region,
    /// partial or invalid components yield an error.
    /// </summary>
    public static bool TryCreate(int? x, int? y, int? width, int? height, string subject, out ScreenRect? region, [NotNullWhen(false)] out string? error)
    {
        region = null;
        error = null;
        if (x is null && y is null && width is null && height is null)
        {
            return true;
        }

        if (x is not int left || y is not int top || width is not int regionWidth || height is not int regionHeight)
        {
            error = $"{subject} requires <x> <y> <width> <height>.";
            return false;
        }

        error = Validate(left, top, regionWidth, regionHeight, subject);
        if (error is not null)
        {
            return false;
        }

        region = new ScreenRect(left, top, regionWidth, regionHeight);
        return true;
    }
}
