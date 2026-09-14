namespace CrossMacro.Infrastructure.Services.Scripting;

internal static partial class RunScriptScreenReadingStepParser
{
    private static ParsedScreenReadStep ParseValidatedParts(RunScriptScreenReadingCommand command, string[] parts) => command switch
    {
        RunScriptScreenReadingCommand.PixelColor => ParsePixelColor(parts),
        RunScriptScreenReadingCommand.WaitColor => new ParsedScreenReadStep.WaitColor(
            ReadInteger(parts[1]), ReadInteger(parts[2]), parts[3], parts.Length > 4 ? ReadInteger(parts[4]) : null,
            parts.Length > 5 ? parts[5] : null),
        RunScriptScreenReadingCommand.PixelSearch => ParsePixelSearch(parts),
        RunScriptScreenReadingCommand.ImageSearch or RunScriptScreenReadingCommand.ImageClick or RunScriptScreenReadingCommand.WaitImage => ParseImage(command, parts),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Screen command is invalid."),
    };

    private static ParsedScreenReadStep.PixelColor ParsePixelColor(string[] parts)
    {
        var relative = string.Equals(parts[1], "rel", StringComparison.OrdinalIgnoreCase);
        var index = relative ? 2 : 1;
        return new ParsedScreenReadStep.PixelColor(relative, ReadInteger(parts[index]), ReadInteger(parts[index + 1]),
            parts.Length > index + 2 ? parts[index + 2] : null);
    }

    private static ParsedScreenReadStep.PixelSearch ParsePixelSearch(string[] parts)
    {
        var variables = GetPixelSearchVariableLayout(parts);
        var index = 6;
        if (variables.FoundVariableName is not null)
        {
            index = 9;
        }
        else if (variables.XVariableName is not null)
        {
            index = 8;
        }
        int? timeout = null;
        var tolerance = 0;
        for (; index < parts.Length; index += 2)
        {
            if (IsScreenReadTimeoutKeyword(parts[index]))
            {
                timeout = ReadInteger(parts[index + 1]);
            }
            else
            {
                tolerance = ReadInteger(parts[index + 1]);
            }
        }
        return new ParsedScreenReadStep.PixelSearch(ReadInteger(parts[1]), ReadInteger(parts[2]),
            ReadInteger(parts[3]), ReadInteger(parts[4]), parts[5], variables, timeout, tolerance);
    }

    private static ParsedScreenReadStep.Image ParseImage(RunScriptScreenReadingCommand command, string[] parts)
    {
        ParsedScreenReadStep.ImageRegion? region = null;
        var imageIndex = 1;
        if (IsExplicitImageRegion(parts))
        {
            region = new ParsedScreenReadStep.ExplicitRegion(parts[2], parts[3], parts[4], parts[5]);
            imageIndex = 6;
        }
        else if (parts.Length >= 6 && AreIntegerTokens(parts[1], parts[2], parts[3], parts[4]))
        {
            region = new ParsedScreenReadStep.LegacyRegion(ReadInteger(parts[1]), ReadInteger(parts[2]), ReadInteger(parts[3]), ReadInteger(parts[4]));
            imageIndex = 5;
        }

        var index = imageIndex + 1;
        var variableStart = index;
        while (index < parts.Length && !(command is RunScriptScreenReadingCommand.ImageClick
            ? IsImageClickOptionKeyword(parts[index]) : IsImageSearchOptionKeyword(parts[index]))) { index++; }
        var variables = index - variableStart is 3
            ? new PixelSearchVariableLayout(parts[variableStart], parts[variableStart + 1], parts[variableStart + 2])
            : default;
        var similarity = ScreenImageMatchDefaults.Similarity;
        var mode = EditorImageMatchMode.Automatic;
        var modeExplicit = false;
        int? timeout = null;
        var button = MacroMouseButton.Left;
        for (; index < parts.Length; index += 2)
        {
            switch (ScreenReadOptionGrammar.GetScriptOptionKind(parts[index]))
            {
                case ScreenReadOptionKind.Similarity:
                    similarity = double.Parse(parts[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture);
                    break;
                case ScreenReadOptionKind.MatchMode:
                    _ = RunScriptPlatformSyntax.TryParseImageMatchMode(parts[index + 1], out mode);
                    modeExplicit = true;
                    break;
                case ScreenReadOptionKind.Timeout:
                    timeout = ReadInteger(parts[index + 1]);
                    break;
                case ScreenReadOptionKind.Button:
                    _ = RunScriptInputSyntax.TryResolveButton(parts[index + 1], out button);
                    break;
            }
        }
        return new ParsedScreenReadStep.Image(command, parts[imageIndex], region, variables, similarity, mode, modeExplicit, timeout, button);
    }

    private static int ReadInteger(string token) => int.Parse(token, NumberStyles.Integer, CultureInfo.InvariantCulture);
}
