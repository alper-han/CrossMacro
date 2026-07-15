
namespace CrossMacro.Core.Services;

/// <summary>
/// Shared run-script command tokens and small syntax helpers.
/// </summary>
public static class RunScriptSyntax
{
    public readonly record struct ScreenshotStep(
        string? OutputPath,
        bool CopyToClipboard,
        bool UseRegion,
        string RegionX,
        string RegionY,
        string RegionWidth,
        string RegionHeight);

    public const string ElseBlockHeader = "else {";
    public const string BlockEndToken = "}";
    public const string BreakCommand = "break";
    public const string ContinueCommand = "continue";
    public const string CurrentPositionToken = "current";
    public const string PixelColorCommand = "pixelcolor";
    public const string WaitColorCommand = "waitcolor";
    public const string PixelSearchCommand = "pixelsearch";
    public const string ImageSearchCommand = "imagesearch";
    public const string ImageClickCommand = "imageclick";
    public const string WaitImageCommand = "waitimage";
    public const string PixelSearchToleranceKeyword = "tolerance";
    public const string ImageSearchSimilarityKeyword = "similarity";
    public const string ImageSearchDownsampleKeyword = "downsample";
    public const string ImageSearchTimeoutKeyword = "timeout";
    public const string ImageSearchMatchModeKeyword = "matchmode";
    public const string ImageSearchScaleAwareKeyword = "scaleaware";
    public const string WindowCommand = "window";
    public const string ClipboardCommand = "clipboard";
    public const string ShellCommand = "shell";
    public const string ScreenshotCommand = "screenshot";

    public static List<string> SplitQuotedTokens(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var tokens = new List<string>();
        var current = new StringBuilder();
        char? quote = null;
        var tokenStarted = false;

        for (var index = 0; index < input.Length; index++)
        {
            var character = input[index];

            if (character == '\\'
                && quote is not null
                && index + 1 < input.Length
                && input[index + 1] is '"' or '\'' or '\\')
            {
                tokenStarted = true;
                current.Append(input[++index]);
                continue;
            }

            if (quote is null && char.IsWhiteSpace(character))
            {
                if (tokenStarted)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    tokenStarted = false;
                }

                continue;
            }

            if (character is '"' or '\'')
            {
                tokenStarted = true;
                if (quote == character)
                {
                    quote = null;
                    continue;
                }

                if (quote is null)
                {
                    quote = character;
                    continue;
                }
            }

            tokenStarted = true;
            current.Append(character);
        }

        if (quote is not null)
        {
            throw new FormatException("Unterminated quoted token.");
        }

        if (tokenStarted)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    private static readonly string[] ScreenReadingCommands =
    [
        PixelColorCommand,
        WaitColorCommand,
        PixelSearchCommand,
        ImageSearchCommand,
        ImageClickCommand,
        WaitImageCommand,
    ];

    public static bool IsBreakCommand(string step)
    {
        return string.Equals(step?.Trim(), BreakCommand, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsContinueCommand(string step)
    {
        return string.Equals(step?.Trim(), ContinueCommand, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBlockEndToken(string step)
    {
        return string.Equals(step?.Trim(), BlockEndToken, StringComparison.Ordinal);
    }

    public static bool IsElseHeader(string step)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length is 2
&& string.Equals(parts[0], "else", StringComparison.OrdinalIgnoreCase)
&& string.Equals(parts[1], "{", StringComparison.Ordinal);
    }

    public static bool IsCurrentPositionToken(string token)
    {
        return string.Equals(token?.Trim(), CurrentPositionToken, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsScreenReadingStep(string? step)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        var trimmedStep = step.TrimStart();
        foreach (var command in ScreenReadingCommands)
        {
            if (StartsWithCommandToken(trimmedStep, command))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsScreenReadingCommandToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var trimmedToken = token.Trim();
        foreach (var command in ScreenReadingCommands)
        {
            if (string.Equals(trimmedToken, command, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsPixelSearchToleranceKeyword(string? token)
    {
        return string.Equals(token?.Trim(), PixelSearchToleranceKeyword, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsImageSearchSimilarityKeyword(string? token)
    {
        return string.Equals(token?.Trim(), ImageSearchSimilarityKeyword, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsImageSearchDownsampleKeyword(string? token)
    {
        return string.Equals(token?.Trim(), ImageSearchDownsampleKeyword, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsImageSearchTimeoutKeyword(string? token)
    {
        return string.Equals(token?.Trim(), ImageSearchTimeoutKeyword, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsImageSearchScaleAwareKeyword(string? token) =>
        string.Equals(token?.Trim(), ImageSearchScaleAwareKeyword, StringComparison.OrdinalIgnoreCase);

    public static bool IsWindowStep(string? step)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        return StartsWithCommandToken(step.TrimStart(), WindowCommand);
    }

    public static bool IsClipboardStep(string? step)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        return StartsWithCommandToken(step.TrimStart(), ClipboardCommand);
    }

    public static bool IsShellStep(string? step)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        return StartsWithCommandToken(step.TrimStart(), ShellCommand);
    }

    public static bool IsScreenshotStep(string? step)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        return StartsWithCommandToken(step.TrimStart(), ScreenshotCommand);
    }

    public static bool StartsWithCommandToken(string step, string command)
    {
        return step.StartsWith(command, StringComparison.OrdinalIgnoreCase)
            && (step.Length == command.Length || char.IsWhiteSpace(step[command.Length]));
    }

}
