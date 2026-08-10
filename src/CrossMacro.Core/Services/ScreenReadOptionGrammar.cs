namespace CrossMacro.Core.Services;

/// <summary>
/// Canonical option vocabulary shared by screen-reading parsers.
/// The CLI and run-script surfaces intentionally keep their own spellings,
/// but both map to the same typed option kinds before validating values.
/// </summary>
public static class ScreenReadOptionGrammar
{
    public static ScreenReadOptionKind GetScriptOptionKind(string? token) =>
        token?.Trim().ToUpperInvariant() switch
        {
            "REGION" => ScreenReadOptionKind.Region,
            "TOLERANCE" => ScreenReadOptionKind.Tolerance,
            "SIMILARITY" => ScreenReadOptionKind.Similarity,
            "MATCHMODE" => ScreenReadOptionKind.MatchMode,
            "TIMEOUT" => ScreenReadOptionKind.Timeout,
            "BUTTON" => ScreenReadOptionKind.Button,
            _ => ScreenReadOptionKind.Unknown,
        };

    public static ScreenReadOptionKind GetCliOptionKind(string? token) =>
        token?.Trim().ToUpperInvariant() switch
        {
            "--REGION" => ScreenReadOptionKind.Region,
            "--TOLERANCE" => ScreenReadOptionKind.Tolerance,
            "--SIMILARITY" => ScreenReadOptionKind.Similarity,
            "--MATCHMODE" => ScreenReadOptionKind.MatchMode,
            "--TIMEOUT-MS" => ScreenReadOptionKind.Timeout,
            "--BUTTON" => ScreenReadOptionKind.Button,
            _ => ScreenReadOptionKind.Unknown,
        };

    public static bool IsImageMatchOption(ScreenReadOptionKind kind) =>
        kind is ScreenReadOptionKind.Similarity
            or ScreenReadOptionKind.MatchMode;

    public static bool IsImageSearchOption(ScreenReadOptionKind kind) =>
        IsImageMatchOption(kind)
        || kind is ScreenReadOptionKind.Timeout;

    public static bool IsImageClickOption(ScreenReadOptionKind kind) =>
        IsImageSearchOption(kind) || kind is ScreenReadOptionKind.Button;

    public static bool IsPixelSearchOption(ScreenReadOptionKind kind) =>
        kind is ScreenReadOptionKind.Tolerance
            or ScreenReadOptionKind.Timeout;
}
