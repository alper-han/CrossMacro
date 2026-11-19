
namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>
/// Recognizes and validates the screen-reading commands (<c>pixelcolor</c>, <c>waitcolor</c>, <c>pixelsearch</c>,
/// <c>imagesearch</c>, <c>imageclick</c>, <c>waitimage</c>) and parses valid ones into <see cref="ParsedScreenReadStep"/>.
/// Per-command validation lives in the <c>.Pixel</c> and <c>.Image</c> parts, shared token rules in <c>.Tokens</c>.
/// </summary>
internal static partial class RunScriptScreenReadingStepParser
{
    public static bool TryValidateStep(string step, out string? error) => TryParseStep(step, out _, out error);

    public static bool TryParseStep(string step, out ParsedScreenReadStep? parsed, out string? error)
    {
        parsed = null;
        error = null;
        if (!TryParseCommand(step, out var command, out var parts))
        {
            return false;
        }

        var recognized = command switch
        {
            RunScriptScreenReadingCommand.PixelColor => TryValidatePixelColorStep(parts, out error),
            RunScriptScreenReadingCommand.WaitColor => TryValidateWaitColorStep(parts, out error),
            RunScriptScreenReadingCommand.PixelSearch => TryValidatePixelSearchStep(parts, out error),
            RunScriptScreenReadingCommand.ImageSearch => TryValidateImageSearchStep(parts, out error),
            RunScriptScreenReadingCommand.ImageClick => TryValidateImageClickStep(parts, out error),
            RunScriptScreenReadingCommand.WaitImage => TryValidateWaitImageStep(parts, out error),
            _ => false,
        };
        if (recognized && error is null)
        {
            parsed = ParseValidatedParts(command, parts);
        }

        return recognized;
    }

    public static bool TryParseCommand(
        string step,
        out RunScriptScreenReadingCommand command,
        out string[] parts)
    {
        parts = SplitStep(step);
        command = default;
        if (parts.Length is 0)
        {
            return false;
        }

        var name = parts[0];
        if (string.Equals(name, RunScriptSyntax.PixelColorCommand, StringComparison.OrdinalIgnoreCase))
        {
            command = RunScriptScreenReadingCommand.PixelColor;
        }
        else if (string.Equals(name, RunScriptSyntax.WaitColorCommand, StringComparison.OrdinalIgnoreCase))
        {
            command = RunScriptScreenReadingCommand.WaitColor;
        }
        else if (string.Equals(name, RunScriptSyntax.PixelSearchCommand, StringComparison.OrdinalIgnoreCase))
        {
            command = RunScriptScreenReadingCommand.PixelSearch;
        }
        else if (string.Equals(name, RunScriptSyntax.ImageSearchCommand, StringComparison.OrdinalIgnoreCase))
        {
            command = RunScriptScreenReadingCommand.ImageSearch;
        }
        else if (string.Equals(name, RunScriptSyntax.ImageClickCommand, StringComparison.OrdinalIgnoreCase))
        {
            command = RunScriptScreenReadingCommand.ImageClick;
        }
        else if (string.Equals(name, RunScriptSyntax.WaitImageCommand, StringComparison.OrdinalIgnoreCase))
        {
            command = RunScriptScreenReadingCommand.WaitImage;
        }
        else
        {
            parts = [];
            return false;
        }

        return true;
    }

    public static string[] SplitStep(string step)
    {
        return step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static PixelSearchVariableLayout GetPixelSearchVariableLayout(string[] parts)
    {
        if (parts.Length >= 9 && !IsPixelSearchOptionKeyword(parts[6]) && !IsPixelSearchOptionKeyword(parts[8]))
        {
            return new PixelSearchVariableLayout(parts[6], parts[7], parts[8]);
        }

        if (parts.Length >= 8 && !IsPixelSearchOptionKeyword(parts[6]))
        {
            return new PixelSearchVariableLayout(FoundVariableName: null, parts[6], parts[7]);
        }

        return default;
    }

    public static bool IsPixelSearchToleranceKeyword(string value) =>
        RunScriptSyntax.IsPixelSearchToleranceKeyword(value);

    public static bool IsScreenReadTimeoutKeyword(string value) =>
        RunScriptSyntax.IsImageSearchTimeoutKeyword(value);

    public static bool IsPixelSearchOptionKeyword(string value) =>
        ScreenReadOptionGrammar.IsPixelSearchOption(ScreenReadOptionGrammar.GetScriptOptionKind(value))
        || IsRetiredScreenReadOptionKeyword(value);

    public static bool IsImageSearchOptionKeyword(string value) =>
        ScreenReadOptionGrammar.IsImageSearchOption(ScreenReadOptionGrammar.GetScriptOptionKind(value))
        || IsRetiredScreenReadOptionKeyword(value);

    public static bool IsImageMatchOptionKeyword(string value) =>
        ScreenReadOptionGrammar.IsImageMatchOption(ScreenReadOptionGrammar.GetScriptOptionKind(value));

    private static bool IsRetiredScreenReadOptionKeyword(string value) =>
        string.Equals(value, "poll", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "downsample", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "scaleaware", StringComparison.OrdinalIgnoreCase);
}
