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
            "DOWNSAMPLE" => ScreenReadOptionKind.Downsample,
            "MATCHMODE" => ScreenReadOptionKind.MatchMode,
            "SCALEAWARE" => ScreenReadOptionKind.ScaleAware,
            "TIMEOUT" => ScreenReadOptionKind.Timeout,
            "POLL" => ScreenReadOptionKind.Poll,
            "BUTTON" => ScreenReadOptionKind.Button,
            _ => ScreenReadOptionKind.Unknown,
        };

    public static ScreenReadOptionKind GetCliOptionKind(string? token) =>
        token?.Trim().ToUpperInvariant() switch
        {
            "--REGION" => ScreenReadOptionKind.Region,
            "--TOLERANCE" => ScreenReadOptionKind.Tolerance,
            "--SIMILARITY" => ScreenReadOptionKind.Similarity,
            "--DOWNSAMPLE" => ScreenReadOptionKind.Downsample,
            "--MATCHMODE" => ScreenReadOptionKind.MatchMode,
            "--SCALE-AWARE" => ScreenReadOptionKind.ScaleAware,
            "--TIMEOUT-MS" => ScreenReadOptionKind.Timeout,
            "--POLL" => ScreenReadOptionKind.Poll,
            "--POLL-MS" => ScreenReadOptionKind.PollInterval,
            "--BUTTON" => ScreenReadOptionKind.Button,
            _ => ScreenReadOptionKind.Unknown,
        };

    public static bool IsImageMatchOption(ScreenReadOptionKind kind) =>
        kind is ScreenReadOptionKind.Similarity
            or ScreenReadOptionKind.Downsample
            or ScreenReadOptionKind.MatchMode
            or ScreenReadOptionKind.ScaleAware;

    public static bool IsImageSearchOption(ScreenReadOptionKind kind) =>
        IsImageMatchOption(kind)
        || kind is ScreenReadOptionKind.Timeout or ScreenReadOptionKind.Poll;

    public static bool IsImageClickOption(ScreenReadOptionKind kind) =>
        IsImageSearchOption(kind) || kind is ScreenReadOptionKind.Button;

    public static bool IsPixelSearchOption(ScreenReadOptionKind kind) =>
        kind is ScreenReadOptionKind.Tolerance
            or ScreenReadOptionKind.Timeout
            or ScreenReadOptionKind.Poll;
}
