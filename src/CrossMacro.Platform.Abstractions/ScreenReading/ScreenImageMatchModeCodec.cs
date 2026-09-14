namespace CrossMacro.Platform.Abstractions.ScreenReading;

/// <summary>Canonical image-match tokens. Whitespace and missing-value policy belong to the caller.</summary>
public static class ScreenImageMatchModeCodec
{
    public static bool TryParse(string? token, out ScreenImageMatchMode mode)
    {
        if (string.Equals(token, "auto", StringComparison.OrdinalIgnoreCase))
        {
            mode = ScreenImageMatchMode.Automatic;
            return true;
        }
        if (string.Equals(token, "first", StringComparison.OrdinalIgnoreCase))
        {
            mode = ScreenImageMatchMode.First;
            return true;
        }
        if (string.Equals(token, "best", StringComparison.OrdinalIgnoreCase))
        {
            mode = ScreenImageMatchMode.Best;
            return true;
        }
        mode = default;
        return false;
    }

    public static string Format(ScreenImageMatchMode matchMode) => matchMode switch
    {
        ScreenImageMatchMode.Automatic => "auto",
        ScreenImageMatchMode.First => "first",
        ScreenImageMatchMode.Best => "best",
        _ => throw new ArgumentOutOfRangeException(nameof(matchMode), matchMode, "Image match mode is invalid."),
    };
}
