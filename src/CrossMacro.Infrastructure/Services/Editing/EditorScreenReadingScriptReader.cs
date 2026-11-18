using System.Diagnostics.CodeAnalysis;

namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>Projects the shared screen-reading step parse into editable actions.</summary>
internal static class EditorScreenReadingScriptReader
{
    internal static bool TryParseScreenReadingStep(string step, [NotNullWhen(true)] out EditorAction? action)
    {
        action = null;
        if (!RunScriptScreenReadingStepParser.TryParseStep(step, out var parsed, out var error) || error is not null)
        {
            return false;
        }

        action = parsed switch
        {
            ParsedScreenReadStep.PixelColor pixel => ProjectPixelColor(pixel),
            ParsedScreenReadStep.WaitColor wait => ProjectWaitColor(wait),
            ParsedScreenReadStep.PixelSearch search => ProjectPixelSearch(search),
            ParsedScreenReadStep.Image image => ProjectImage(image),
            _ => null,
        };
        return action is not null;
    }

    private static EditorAction ProjectPixelColor(ParsedScreenReadStep.PixelColor step)
    {
        var action = new EditorAction();
        action.ApplyScreenReadingPayload(EditorActionScreenReadingPayload.ForPixelColor(!step.Relative, step.X, step.Y,
            NormalizeVariable(step.ResultVariable, EditorActionScreenReadingPayload.DefaultColorVariableName)));
        return action;
    }

    private static EditorAction? ProjectWaitColor(ParsedScreenReadStep.WaitColor step)
    {
        if (!TryParseTargetColorToken(step.ColorToken, out var source, out var color, out var variable))
        {
            return null;
        }

        var action = new EditorAction();
        action.ApplyScreenReadingPayload(EditorActionScreenReadingPayload.ForWaitColor(step.X, step.Y, color,
            step.TimeoutMs ?? EditorActionScreenReadingPayload.DefaultTimeoutMs,
            NormalizeVariable(step.ResultVariable, EditorActionScreenReadingPayload.DefaultColorVariableName)));
        action.ScreenTargetColorSource = source;
        action.ScreenTargetColorVariableName = variable;
        return action;
    }

    private static EditorAction? ProjectPixelSearch(ParsedScreenReadStep.PixelSearch step)
    {
        if (!TryParseTargetColorToken(step.ColorToken, out var source, out var color, out var variable)
            || !TryGetPositiveRegionSize(step.X1, step.Y1, step.X2, step.Y2, out var width, out var height))
        {
            return null;
        }

        var action = new EditorAction();
        action.ApplyScreenReadingPayload(EditorActionScreenReadingPayload.ForPixelSearch(step.X1, step.Y1, width, height, color,
            NormalizeVariable(step.Variables.FoundVariableName, EditorActionScreenReadingPayload.DefaultFoundVariableName),
            NormalizeVariable(step.Variables.XVariableName, EditorActionScreenReadingPayload.DefaultFoundXVariableName),
            NormalizeVariable(step.Variables.YVariableName, EditorActionScreenReadingPayload.DefaultFoundYVariableName),
            step.Tolerance));
        action.ScreenTimeoutMs = step.TimeoutMs ?? EditorActionScreenReadingPayload.DefaultTimeoutMs;
        action.ScreenTargetColorSource = source;
        action.ScreenTargetColorVariableName = variable;
        return action;
    }

    private static EditorAction ProjectImage(ParsedScreenReadStep.Image step)
    {
        var legacy = step.Region as ParsedScreenReadStep.LegacyRegion;
        var action = new EditorAction
        {
            Type = step.Command switch
            {
                RunScriptScreenReadingCommand.ImageClick => EditorActionType.ImageClick,
                RunScriptScreenReadingCommand.WaitImage => EditorActionType.WaitImage,
                RunScriptScreenReadingCommand.ImageSearch => EditorActionType.ImageSearch,
                RunScriptScreenReadingCommand.PixelColor or RunScriptScreenReadingCommand.WaitColor or RunScriptScreenReadingCommand.PixelSearch
                    => throw new ArgumentOutOfRangeException(nameof(step), step.Command, "Expected an image command."),
                _ => throw new ArgumentOutOfRangeException(nameof(step), step.Command, "Screen command is invalid."),
            },
            ScreenLeft = legacy?.Left ?? 0,
            ScreenTop = legacy?.Top ?? 0,
            ScreenWidth = legacy is null ? EditorActionScreenReadingPayload.DefaultSearchScreenWidth : legacy.Right - legacy.Left,
            ScreenHeight = legacy is null ? EditorActionScreenReadingPayload.DefaultSearchScreenHeight : legacy.Bottom - legacy.Top,
            ImageAssetName = step.ImageName,
            ScreenFoundVariableName = NormalizeVariable(step.Variables.FoundVariableName, EditorActionScreenReadingPayload.DefaultFoundVariableName),
            ScreenFoundXVariableName = NormalizeVariable(step.Variables.XVariableName, EditorActionScreenReadingPayload.DefaultFoundXVariableName),
            ScreenFoundYVariableName = NormalizeVariable(step.Variables.YVariableName, EditorActionScreenReadingPayload.DefaultFoundYVariableName),
            ScreenTimeoutMs = step.TimeoutMs ?? EditorActionScreenReadingPayload.DefaultTimeoutMs,
            ImageSearchSimilarity = step.Similarity,
            ImageSearchMatchMode = step.MatchMode,
            ImageSearchMatchModeWasExplicit = step.MatchModeExplicit,
            Button = step.Button,
        };
        if (step.Region is ParsedScreenReadStep.ExplicitRegion region)
        {
            action.ImageSearchRegionLeftToken = region.Left;
            action.ImageSearchRegionTopToken = region.Top;
            action.ImageSearchRegionWidthToken = region.Width;
            action.ImageSearchRegionHeightToken = region.Height;
        }

        return action;
    }

    private static string NormalizeVariable(string? name, string defaultName) =>
        EditorActionScriptTokens.NormalizeVariableToken(name ?? defaultName);

    private static bool TryGetPositiveRegionSize(int left, int top, int right, int bottom, out int width, out int height)
    {
        var widthValue = (long)right - left;
        var heightValue = (long)bottom - top;
        if (widthValue <= 0 || heightValue <= 0 || widthValue > int.MaxValue || heightValue > int.MaxValue)
        {
            width = 0;
            height = 0;
            return false;
        }

        width = (int)widthValue;
        height = (int)heightValue;
        return true;
    }

    private static bool TryParseTargetColorToken(string token, out EditorActionScreenTargetColorSource colorSource, out string colorHex, out string variableName)
    {
        colorSource = EditorActionScreenTargetColorSource.ManualHex;
        colorHex = EditorActionScreenReadingPayload.DefaultColorHex;
        variableName = EditorActionScreenReadingPayload.DefaultTargetColorVariableName;
        if (ScreenPixelColor.TryParse(token, out var color))
        {
            colorHex = color.ToString();
            return true;
        }

        if (!token.StartsWith('$') || !EditorScriptTokenReader.TryNormalizeVariableName(token, out variableName))
        {
            return false;
        }

        colorSource = EditorActionScreenTargetColorSource.Variable;
        return true;
    }
}
