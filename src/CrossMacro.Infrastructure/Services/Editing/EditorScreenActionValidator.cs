namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>Validates pixel, image and screenshot actions.</summary>
internal static class EditorScreenActionValidator
{
    internal static (bool IsValid, string? Error) ValidatePixelColor(EditorAction action)
    {
        var payload = GetScreenReadingPayload(action);

        if (!payload.HasValidColorVariableName())
        {
            return (false, "Pixel color output variable name is invalid.");
        }

        if (payload.ScreenTimeoutMs < 0)
        {
            return (false, "Pixel color timeout must be >= 0.");
        }

        return (true, null);
    }

    internal static (bool IsValid, string? Error) ValidateWaitColor(EditorAction action)
    {
        var payload = GetScreenReadingPayload(action);

        if (!payload.HasValidTargetColor())
        {
            return payload.ScreenTargetColorSource is EditorActionScreenTargetColorSource.Variable
                ? (false, "Wait color target variable name is invalid.")
                : (false, "Wait color target must be 6 hexadecimal RGB characters.");
        }

        if (payload.ScreenTimeoutMs < 0)
        {
            return (false, "Wait color timeout must be non-negative.");
        }

        if (!payload.HasValidColorVariableName())
        {
            return (false, "Wait color result variable name is invalid.");
        }

        return (true, null);
    }

    internal static (bool IsValid, string? Error) ValidatePixelSearch(EditorAction action)
    {
        var payload = GetScreenReadingPayload(action);

        if (!payload.HasPositiveSearchRegion())
        {
            return (false, "Pixel search region size must be positive.");
        }

        if (!HasCheckedRegionEndpoints(payload.ScreenLeft, payload.ScreenTop, payload.ScreenWidth, payload.ScreenHeight))
        {
            return (false, "Pixel search region endpoint exceeds the supported screen coordinate range.");
        }

        if (!payload.HasValidTargetColor())
        {
            return payload.ScreenTargetColorSource is EditorActionScreenTargetColorSource.Variable
                ? (false, "Pixel search target variable name is invalid.")
                : (false, "Pixel search target must be 6 hexadecimal RGB characters.");
        }

        if (!payload.HasValidTolerance())
        {
            return (false, "Pixel search tolerance must be between 0 and 255.");
        }

        if (payload.ScreenTimeoutMs < 0)
        {
            return (false, "Pixel search timeout must be >= 0.");
        }

        if (!payload.HasValidFoundVariableName() || !payload.HasValidFoundCoordinateVariableNames())
        {
            return (false, "Pixel search output variable names are invalid.");
        }

        return (true, null);
    }

    internal static (bool IsValid, string? Error) ValidateImageSearch(EditorAction action)
    {
        if (!action.HasValidImageSearchRegionTokens())
        {
            return (false, "Image search region values must be integers or variables, with a positive width and height.");
        }

        if (action.TryGetLiteralImageSearchRegion(out var left, out var top, out var width, out var height)
            && !HasCheckedRegionEndpoints(left, top, width, height))
        {
            return (false, "Image search region endpoint exceeds the supported screen coordinate range.");
        }

        if (!ScriptIdentifierSyntax.IsValidVariableName(action.ImageAssetName))
        {
            return (false, "Image search asset name is invalid.");
        }

        if (!ScriptIdentifierSyntax.IsValidVariableName(action.ScreenFoundVariableName)
            || !ScriptIdentifierSyntax.IsValidVariableName(action.ScreenFoundXVariableName)
            || !ScriptIdentifierSyntax.IsValidVariableName(action.ScreenFoundYVariableName))
        {
            return (false, "Image search output variable names are invalid.");
        }

        if (!double.IsFinite(action.ImageSearchSimilarity) || action.ImageSearchSimilarity is < 0.0 or > 1.0)
        {
            return (false, "Image search similarity must be between 0.0 and 1.0.");
        }

        if (!Enum.IsDefined(action.ImageSearchMatchMode))
        {
            return (false, "Image search match mode is invalid.");
        }

        if (action.ScreenTimeoutMs < 0)
        {
            return (false, "Image search timeout must be >= 0.");
        }

        if (action.Type is EditorActionType.ImageClick
            && action.Button is not (MacroMouseButton.Left or MacroMouseButton.Right or MacroMouseButton.Middle))
        {
            return (false, "Image click button must be left, right, or middle.");
        }

        return (true, null);
    }

    internal static (bool IsValid, string? Error) ValidateScreenshot(EditorAction action)
    {
        if (string.IsNullOrWhiteSpace(action.ScreenshotOutputPath) && !action.ScreenshotCopyToClipboard)
        {
            return (false, "Screenshot requires an output path or clipboard destination.");
        }

        if (!action.ScreenshotUseRegion)
        {
            return (true, null);
        }

        if (!IsIntegerOrVariable(action.ScreenshotRegionX) || !IsIntegerOrVariable(action.ScreenshotRegionY))
        {
            return (false, "Screenshot region x/y must be integers or variables.");
        }

        if (!IsPositiveIntegerOrVariable(action.ScreenshotRegionWidth) || !IsPositiveIntegerOrVariable(action.ScreenshotRegionHeight))
        {
            return (false, "Screenshot region width/height must be positive integers or variables.");
        }

        if (int.TryParse(action.ScreenshotRegionX, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
            && int.TryParse(action.ScreenshotRegionY, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
            && int.TryParse(action.ScreenshotRegionWidth, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
            && int.TryParse(action.ScreenshotRegionHeight, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
            && !HasCheckedRegionEndpoints(x, y, width, height))
        {
            return (false, "Screenshot region endpoint exceeds the supported screen coordinate range.");
        }

        return (true, null);
    }

    private static EditorActionScreenReadingPayload GetScreenReadingPayload(EditorAction action)
    {
        if (!action.TryGetScreenReadingPayload(out var payload))
        {
            throw new InvalidOperationException("Action type does not contain a screen-reading payload.");
        }

        return payload;
    }

    private static bool IsIntegerOrVariable(string token)
    {
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            || (token.StartsWith('$') && ScriptIdentifierSyntax.IsValidVariableName(token));
    }

    private static bool IsPositiveIntegerOrVariable(string token)
    {
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value > 0
            : token.StartsWith('$') && ScriptIdentifierSyntax.IsValidVariableName(token);
    }

    private static bool HasCheckedRegionEndpoints(int left, int top, int width, int height)
    {
        try
        {
            _ = checked(left + width);
            _ = checked(top + height);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
