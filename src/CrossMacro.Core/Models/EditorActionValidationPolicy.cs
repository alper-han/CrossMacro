
namespace CrossMacro.Core.Models;

/// <summary>
/// Contains the validation rules for an editor action.
/// </summary>
/// <remarks>
/// EditorAction remains the binding-friendly state holder. Keeping validation
/// here makes the editor projection policy explicit without moving the public
/// model or changing any of its serialization and binding members.
/// </remarks>
internal static class EditorActionValidationPolicy
{
    internal static bool IsScriptPayloadAction(EditorActionType type)
    {
        return type is
            EditorActionType.SetVariable
            or EditorActionType.IncrementVariable
            or EditorActionType.DecrementVariable
            or EditorActionType.MultiplyVariable
            or EditorActionType.DivideVariable
            or EditorActionType.RepeatBlockStart
            or EditorActionType.IfBlockStart
            or EditorActionType.WhileBlockStart
            or EditorActionType.ForBlockStart
            or EditorActionType.PixelColor
            or EditorActionType.WaitColor
            or EditorActionType.PixelSearch
            or EditorActionType.ImageSearch
            or EditorActionType.ImageClick
            or EditorActionType.WaitImage
            or EditorActionType.MousePosition
            or EditorActionType.ClipboardGet
            or EditorActionType.ClipboardSet
            or EditorActionType.ShellCommand
            or EditorActionType.Screenshot
            or EditorActionType.WindowCommand;
    }

    internal static bool IsValid(EditorAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return action.Type switch
        {
            EditorActionType.Delay when action.UseRandomDelay =>
                action.RandomDelayMinMs >= 0
                && action.RandomDelayMaxMs >= action.RandomDelayMinMs
                && !(action.RandomDelayMinMs is 0 && action.RandomDelayMaxMs is 0),
            EditorActionType.Delay => action.DelayMicroseconds >= 0,
            EditorActionType.KeyPress or EditorActionType.KeyDown or EditorActionType.KeyUp => action.KeyCode > 0,
            EditorActionType.ScrollVertical or EditorActionType.ScrollHorizontal => action.ScrollAmount is not 0,
            EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp when action.UseCurrentPosition => !action.IsAbsolute,
            EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp => ValidateCoordinateTokens(action),
            EditorActionType.TextInput => !string.IsNullOrEmpty(action.Text),
            EditorActionType.SetVariable => UsesLegacyScriptText(action) || ValidateSetVariableFields(action),
            EditorActionType.IncrementVariable or EditorActionType.DecrementVariable or EditorActionType.MultiplyVariable or EditorActionType.DivideVariable => UsesLegacyScriptText(action) || ValidateIncDecFields(action),
            EditorActionType.RepeatBlockStart => UsesLegacyScriptText(action) || ValidateRepeatFields(action),
            EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart => UsesLegacyScriptText(action) || ValidateConditionFields(action),
            EditorActionType.ForBlockStart => UsesLegacyScriptText(action) || ValidateForFields(action),
            EditorActionType.PixelColor => ValidatePixelColorFields(action),
            EditorActionType.WaitColor => ValidateWaitColorFields(action),
            EditorActionType.PixelSearch => ValidatePixelSearchFields(action),
            EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage => ValidateImageSearchFields(action),
            EditorActionType.MousePosition => ValidateMousePositionFields(action),
            EditorActionType.ClipboardGet => EditorActionScriptTokens.IsValidVariableName(action.ScriptVariableName),
            EditorActionType.ClipboardSet => !string.IsNullOrEmpty(action.Text),
            EditorActionType.ShellCommand => ValidateShellCommandFields(action),
            EditorActionType.Screenshot => ValidateScreenshotFields(action),
            EditorActionType.WindowCommand => ValidateWindowCommandFields(action),
            EditorActionType.RawScriptStep => !string.IsNullOrWhiteSpace(action.Text),
            EditorActionType.ElseBlockStart or EditorActionType.BlockEnd or EditorActionType.Break or EditorActionType.Continue => true,
            EditorActionType.MouseMove => ValidateCoordinateTokens(action),
            _ => true,
        };
    }

    private static bool UsesLegacyScriptText(EditorAction action) =>
        action.PreferLegacyScriptText && !string.IsNullOrWhiteSpace(action.Text);

    private static bool ValidateSetVariableFields(EditorAction action)
    {
        if (!EditorActionScriptTokens.IsValidVariableName(action.ScriptVariableName))
        {
            return false;
        }

        return action.ScriptValueType switch
        {
            ScriptValueType.Number => int.TryParse(action.ScriptValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            ScriptValueType.Boolean => bool.TryParse(action.ScriptValue, out _),
            ScriptValueType.Text => !string.IsNullOrWhiteSpace(action.ScriptValue),
            ScriptValueType.VariableReference => EditorActionScriptTokens.IsValidVariableName(action.ScriptValue),
            _ => false,
        };
    }

    private static bool ValidateCoordinateTokens(EditorAction action)
    {
        return EditorActionScriptTokens.TryParseNumericToken(action.CoordinateXToken, out _, out _)
            && EditorActionScriptTokens.TryParseNumericToken(action.CoordinateYToken, out _, out _);
    }

    private static bool ValidateMousePositionFields(EditorAction action)
    {
        return EditorActionScriptTokens.IsValidVariableName(action.MousePositionXVariableName)
            && EditorActionScriptTokens.IsValidVariableName(action.MousePositionYVariableName)
            && !string.Equals(action.MousePositionXVariableName, action.MousePositionYVariableName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValidateIncDecFields(EditorAction action)
    {
        return EditorActionScriptTokens.IsValidVariableName(action.ScriptVariableName)
            && EditorActionScriptTokens.ValidateNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue);
    }

    private static bool ValidateRepeatFields(EditorAction action)
    {
        return EditorActionScriptTokens.ValidateBlockNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue);
    }

    private static bool ValidateConditionFields(EditorAction action)
    {
        return EditorActionScriptTokens.ValidateOperandToken(action.ScriptLeftOperandType, action.ScriptLeftOperand)
            && EditorActionScriptTokens.ValidateOperandToken(action.ScriptRightOperandType, action.ScriptRightOperand);
    }

    private static bool ValidateForFields(EditorAction action)
    {
        if (!EditorActionScriptTokens.IsValidVariableName(action.ForVariableName))
        {
            return false;
        }

        if (!EditorActionScriptTokens.ValidateBlockNumericToken(action.ForStartType, action.ForStartValue)
            || !EditorActionScriptTokens.ValidateBlockNumericToken(action.ForEndType, action.ForEndValue))
        {
            return false;
        }

        return !action.ForHasStep
            || EditorActionScriptTokens.ValidateBlockNumericToken(action.ForStepType, action.ForStepValue);
    }

    private static bool ValidatePixelColorFields(EditorAction action)
    {
        return action.TryGetScreenReadingPayload(out var payload) && payload.HasValidColorVariableName();
    }

    private static bool ValidateWaitColorFields(EditorAction action)
    {
        return action.TryGetScreenReadingPayload(out var payload)
            && payload.HasValidTargetColor()
            && payload.ScreenTimeoutMs >= 0;
    }

    private static bool ValidatePixelSearchFields(EditorAction action)
    {
        return action.TryGetScreenReadingPayload(out var payload)
            && payload.HasValidTargetColor()
            && payload.HasPositiveSearchRegion()
            && payload.HasValidTolerance()
            && payload.HasValidFoundCoordinateVariableNames();
    }

    private static bool ValidateImageSearchFields(EditorAction action)
    {
        return EditorActionScriptTokens.IsValidVariableName(action.ImageAssetName)
            && action.ScreenWidth > 0
            && action.ScreenHeight > 0
            && EditorActionScriptTokens.IsValidVariableName(action.ScreenFoundVariableName)
            && EditorActionScriptTokens.IsValidVariableName(action.ScreenFoundXVariableName)
            && EditorActionScriptTokens.IsValidVariableName(action.ScreenFoundYVariableName)
            && double.IsFinite(action.ImageSearchSimilarity)
            && action.ImageSearchSimilarity is >= 0.0 and <= 1.0
            && (action.Type is not EditorActionType.ImageClick
                || action.Button is MacroMouseButton.Left or MacroMouseButton.Right or MacroMouseButton.Middle);
    }

    private static bool ValidateShellCommandFields(EditorAction action)
    {
        if (string.IsNullOrWhiteSpace(action.ShellCommand)
            || action.ShellRetries < 0
            || action.ShellRetries > 10_000
            || action.ShellBackoffMs < 0
            || action.ShellTimeoutMs < 0)
        {
            return false;
        }

        if (action.ShellCommandMode is ShellCommandMode.ShellCapture or ShellCommandMode.ShellCaptureInput)
        {
            return IsValidShellCaptureTarget(action.ShellExitCodeVariableName)
                && IsValidShellCaptureTarget(action.ShellStandardOutputVariableName)
                && IsValidShellCaptureTarget(action.ShellStandardErrorVariableName);
        }

        return true;
    }

    private static bool ValidateScreenshotFields(EditorAction action)
    {
        if (string.IsNullOrWhiteSpace(action.ScreenshotOutputPath) && !action.ScreenshotCopyToClipboard)
        {
            return false;
        }

        return !action.ScreenshotUseRegion || (IsIntegerOrVariable(action.ScreenshotRegionX)
            && IsIntegerOrVariable(action.ScreenshotRegionY)
            && IsPositiveIntegerOrVariable(action.ScreenshotRegionWidth)
            && IsPositiveIntegerOrVariable(action.ScreenshotRegionHeight));
    }

    private static bool ValidateWindowCommandFields(EditorAction action)
    {
        return action.WindowCommandMode switch
        {
            WindowCommandMode.Active => IsValidWindowActiveField(action.WindowActiveField)
                && EditorActionScriptTokens.IsValidVariableName(action.WindowOutputVariable),
            WindowCommandMode.Search => IsValidWindowSearchSelector(action.WindowSelectorKind)
                && !string.IsNullOrWhiteSpace(action.WindowSelectorValue)
                && EditorActionScriptTokens.IsValidVariableName(action.WindowOutputVariable),
            WindowCommandMode.Wait => IsValidWindowSearchSelector(action.WindowSelectorKind)
                && !string.IsNullOrWhiteSpace(action.WindowSelectorValue)
                && action.WindowTimeoutMs > 0
                && EditorActionScriptTokens.IsValidVariableName(action.WindowOutputVariable),
            WindowCommandMode.Focus => string.Equals(action.WindowSelectorKind, "active", StringComparison.Ordinal)
                || (IsValidWindowFocusSelector(action.WindowSelectorKind) && !string.IsNullOrWhiteSpace(action.WindowSelectorValue)),
            WindowCommandMode.Close => string.Equals(action.WindowSelectorKind, "active", StringComparison.Ordinal)
                || (IsValidWindowCloseSelector(action.WindowSelectorKind) && !string.IsNullOrWhiteSpace(action.WindowSelectorValue)),
            WindowCommandMode.Resize => action.WindowWidth > 0 && action.WindowHeight > 0,
            WindowCommandMode.WorkspaceGet => EditorActionScriptTokens.IsValidVariableName(action.WindowOutputVariable),
            WindowCommandMode.WorkspaceSwitch or WindowCommandMode.WorkspaceMoveActive => !string.IsNullOrWhiteSpace(action.WindowWorkspace),
            WindowCommandMode.WorkspaceMoveWindow => !string.IsNullOrWhiteSpace(action.WindowSelectorValue)
                && !string.IsNullOrWhiteSpace(action.WindowWorkspace),
            WindowCommandMode.Move
                or WindowCommandMode.Center
                or WindowCommandMode.Maximize
                or WindowCommandMode.Fullscreen
                or WindowCommandMode.Floating => true,
            _ => true,
        };
    }

    private static bool IsIntegerOrVariable(string token)
    {
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            || (token.StartsWith('$') && EditorActionScriptTokens.IsValidVariableName(token));
    }

    private static bool IsPositiveIntegerOrVariable(string token)
    {
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value > 0
            : token.StartsWith('$') && EditorActionScriptTokens.IsValidVariableName(token);
    }

    private static bool IsValidShellCaptureTarget(string target)
    {
        return string.Equals(target, "_", StringComparison.Ordinal)
            || EditorActionScriptTokens.IsValidVariableName(target);
    }

    private static bool IsValidWindowActiveField(string value)
    {
        return string.Equals(value, "title", StringComparison.Ordinal)
            || string.Equals(value, "class", StringComparison.Ordinal)
            || string.Equals(value, "address", StringComparison.Ordinal)
            || string.Equals(value, "fullscreen", StringComparison.Ordinal)
            || string.Equals(value, "maximize", StringComparison.Ordinal)
            || string.Equals(value, "float", StringComparison.Ordinal)
            || string.Equals(value, "pinned", StringComparison.Ordinal)
            || string.Equals(value, "hidden", StringComparison.Ordinal)
            || string.Equals(value, "geometry", StringComparison.Ordinal);
    }

    private static bool IsValidWindowSearchSelector(string value)
    {
        return string.Equals(value, "title", StringComparison.Ordinal)
            || string.Equals(value, "class", StringComparison.Ordinal);
    }

    private static bool IsValidWindowFocusSelector(string value)
    {
        return string.Equals(value, "title", StringComparison.Ordinal)
            || string.Equals(value, "class", StringComparison.Ordinal)
            || string.Equals(value, "address", StringComparison.Ordinal);
    }

    private static bool IsValidWindowCloseSelector(string value)
    {
        return string.Equals(value, "title", StringComparison.Ordinal)
            || string.Equals(value, "address", StringComparison.Ordinal);
    }
}
