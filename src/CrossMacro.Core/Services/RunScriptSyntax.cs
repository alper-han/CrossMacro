
namespace CrossMacro.Core.Services;

/// <summary>
/// Shared run-script command tokens and small syntax helpers.
/// </summary>
public static class RunScriptSyntax
{
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
    public const string ImageSearchTimeoutKeyword = "timeout";
    public const string ImageSearchMatchModeKeyword = "matchmode";
    public const string WindowCommand = "window";
    public const string ClipboardCommand = "clipboard";
    public const string ShellCommand = "shell";
    public const string ScreenshotCommand = "screenshot";

    public static bool TryParseMouseMoveMode(
        string? token,
        out MouseCoordinateMode coordinateMode,
        out MouseCoordinateSpace coordinateSpace)
    {
        coordinateMode = MouseCoordinateMode.Relative;
        coordinateSpace = MouseCoordinateSpace.RawDevice;

        if (string.Equals(token, "abs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "absolute", StringComparison.OrdinalIgnoreCase))
        {
            coordinateMode = MouseCoordinateMode.Absolute;
            coordinateSpace = MouseCoordinateSpace.LogicalDesktop;
            return true;
        }

        if (string.Equals(token, "rel", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "relative", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(token, "rel-logical", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "relative-logical", StringComparison.OrdinalIgnoreCase))
        {
            coordinateSpace = MouseCoordinateSpace.LogicalDesktop;
            return true;
        }

        if (string.Equals(token, "rel-raw", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "relative-raw", StringComparison.OrdinalIgnoreCase))
        {
            coordinateSpace = MouseCoordinateSpace.RawDevice;
            return true;
        }

        return false;
    }

    public static string ToMouseMoveModeToken(
        MouseCoordinateMode coordinateMode,
        MouseCoordinateSpace coordinateSpace)
    {
        return coordinateMode switch
        {
            MouseCoordinateMode.Absolute => "abs",
            MouseCoordinateMode.Relative when coordinateSpace is MouseCoordinateSpace.LogicalDesktop => "rel-logical",
            MouseCoordinateMode.Relative when coordinateSpace is MouseCoordinateSpace.RawDevice => "rel-raw",
            _ => throw new ArgumentOutOfRangeException(nameof(coordinateMode), coordinateMode, "Mouse coordinate mode is invalid."),
        };
    }

    public static IReadOnlyList<string> SplitQuotedTokens(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var tokens = new List<string>();
        var current = new StringBuilder();
        char? quote = null;
        var tokenStarted = false;

        var index = 0;
        while (index < input.Length)
        {
            var character = input[index];

            if (TryAppendEscapedCharacter(input, ref index, quote, current))
            {
                tokenStarted = true;
                continue;
            }

            if (quote is null && char.IsWhiteSpace(character))
            {
                FlushToken(tokens, current, ref tokenStarted);
                index++;
                continue;
            }

            if (character is '"' or '\'')
            {
                tokenStarted = true;
                if (quote == character)
                {
                    quote = null;
                    index++;
                    continue;
                }

                if (quote is null)
                {
                    quote = character;
                    index++;
                    continue;
                }
            }

            tokenStarted = true;
            current.Append(character);
            index++;
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

    private static bool TryAppendEscapedCharacter(
        string input,
        ref int index,
        char? quote,
        StringBuilder current)
    {
        if (input[index] != '\\'
            || quote is null
            || index + 1 >= input.Length
            || input[index + 1] is not '"' and not '\'' and not '\\')
        {
            return false;
        }

        index++;
        current.Append(input[index]);
        index++;
        return true;
    }

    private static void FlushToken(List<string> tokens, StringBuilder current, ref bool tokenStarted)
    {
        if (!tokenStarted)
        {
            return;
        }

        tokens.Add(current.ToString());
        current.Clear();
        tokenStarted = false;
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
        return ScreenReadingCommands.Any(command => StartsWithCommandToken(trimmedStep, command));
    }

    public static bool IsScreenReadingCommandToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var trimmedToken = token.Trim();
        return ScreenReadingCommands.Any(command => string.Equals(trimmedToken, command, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsPixelSearchToleranceKeyword(string? token)
    {
        return ScreenReadOptionGrammar.GetScriptOptionKind(token) is ScreenReadOptionKind.Tolerance;
    }

    public static bool IsImageSearchSimilarityKeyword(string? token)
    {
        return ScreenReadOptionGrammar.GetScriptOptionKind(token) is ScreenReadOptionKind.Similarity;
    }

    public static bool IsImageSearchTimeoutKeyword(string? token)
    {
        return ScreenReadOptionGrammar.GetScriptOptionKind(token) is ScreenReadOptionKind.Timeout;
    }

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
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(command);

        return step.StartsWith(command, StringComparison.OrdinalIgnoreCase)
            && (step.Length == command.Length || char.IsWhiteSpace(step[command.Length]));
    }

}
