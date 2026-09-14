namespace CrossMacro.Core.Models.Editing;
public partial class EditorAction
{
    private static class ActionDisplayFormatter
    {
        public static string Format(EditorAction action)
        {
            return action.Type switch
            {
                EditorActionType.MouseMove when action.IsAbsolute => $"Move to ({action.CoordinateXToken}, {action.CoordinateYToken})",
                EditorActionType.MouseMove => $"Move by ({FormatRelativeCoordinateToken(action.CoordinateXToken)}, {FormatRelativeCoordinateToken(action.CoordinateYToken)})",
                EditorActionType.MouseClick when action.UseCurrentPosition => $"Click {action.Button} at current position",
                EditorActionType.MouseClick when action.IsAbsolute => $"Click {action.Button} at ({action.CoordinateXToken}, {action.CoordinateYToken})",
                EditorActionType.MouseClick => $"Click {action.Button} by ({FormatRelativeCoordinateToken(action.CoordinateXToken)}, {FormatRelativeCoordinateToken(action.CoordinateYToken)})",
                EditorActionType.MouseDown when action.UseCurrentPosition => $"Hold {action.Button} at current position",
                EditorActionType.MouseDown when action.IsAbsolute => $"Hold {action.Button} at ({action.CoordinateXToken}, {action.CoordinateYToken})",
                EditorActionType.MouseDown => $"Hold {action.Button} by ({FormatRelativeCoordinateToken(action.CoordinateXToken)}, {FormatRelativeCoordinateToken(action.CoordinateYToken)})",
                EditorActionType.MouseUp when action.UseCurrentPosition => $"Release {action.Button} at current position",
                EditorActionType.MouseUp when action.IsAbsolute => $"Release {action.Button} at ({action.CoordinateXToken}, {action.CoordinateYToken})",
                EditorActionType.MouseUp => $"Release {action.Button} by ({FormatRelativeCoordinateToken(action.CoordinateXToken)}, {FormatRelativeCoordinateToken(action.CoordinateYToken)})",
                EditorActionType.MousePosition => GetMousePositionDisplayName(action),
                EditorActionType.KeyPress => $"Press '{action.KeyName ?? action.KeyCode.ToString(CultureInfo.CurrentCulture)}'",
                EditorActionType.KeyDown => $"Hold '{action.KeyName ?? action.KeyCode.ToString(CultureInfo.CurrentCulture)}'",
                EditorActionType.KeyUp => $"Release '{action.KeyName ?? action.KeyCode.ToString(CultureInfo.CurrentCulture)}'",
                EditorActionType.Delay when action.UseRandomDelay => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Wait {action.RandomDelayMinMs}-{action.RandomDelayMaxMs}ms (random)"),
                EditorActionType.Delay => $"Wait {action.DelayDuration}",
                EditorActionType.ScrollVertical => action.ScrollAmount > 0 ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Scroll Up {action.ScrollAmount}") : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Scroll Down {Math.Abs(action.ScrollAmount)}"),
                EditorActionType.ScrollHorizontal => action.ScrollAmount > 0 ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Scroll Right {action.ScrollAmount}") : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Scroll Left {Math.Abs(action.ScrollAmount)}"),
                EditorActionType.TextInput => GetTextInputDisplayName(action),
                EditorActionType.SetVariable => GetSetVariableDisplayName(action),
                EditorActionType.IncrementVariable => GetIncrementVariableDisplayName(action),
                EditorActionType.DecrementVariable => GetDecrementVariableDisplayName(action),
                EditorActionType.MultiplyVariable => GetMultiplyVariableDisplayName(action),
                EditorActionType.DivideVariable => GetDivideVariableDisplayName(action),
                EditorActionType.RepeatBlockStart => action.UseLegacyScriptTextDisplay ? $"Repeat ({action.Text})" : $"Repeat ({BuildNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue)})",
                EditorActionType.IfBlockStart => action.UseLegacyScriptTextDisplay ? $"If ({action.Text})" : $"If ({action.BuildConditionPreview()})",
                EditorActionType.ElseBlockStart => "Else Block",
                EditorActionType.WhileBlockStart => action.UseLegacyScriptTextDisplay ? $"While ({action.Text})" : $"While ({action.BuildConditionPreview()})",
                EditorActionType.ForBlockStart => action.UseLegacyScriptTextDisplay ? $"For ({action.Text})" : action.BuildForPreview(),
                EditorActionType.PixelColor => BuildPixelColorDisplayName(action.ScreenReadingPayload),
                EditorActionType.WaitColor => BuildWaitColorDisplayName(action.ScreenReadingPayload),
                EditorActionType.PixelSearch => BuildPixelSearchDisplayName(action.ScreenReadingPayload),
                EditorActionType.ImageSearch => BuildImageSearchDisplayName(action),
                EditorActionType.ImageClick => BuildImageClickDisplayName(action),
                EditorActionType.WaitImage => BuildWaitImageDisplayName(action),
                EditorActionType.ShellCommand => GetShellCommandDisplayName(action),
                EditorActionType.Screenshot => BuildScreenshotDisplayName(action),
                EditorActionType.WindowCommand => BuildWindowCommandDisplayName(action),
                EditorActionType.CopySelectionToVariable => $"Copy selection with {ClipboardCopyShortcutSyntax.ToScriptToken(action.ClipboardCopyShortcut)} into {action.ScriptVariableName}",
                EditorActionType.Break => "Break",
                EditorActionType.Continue => "Continue",
                EditorActionType.BlockEnd => "End Block",
                EditorActionType.RawScriptStep => GetRawScriptStepDisplayName(action),
                EditorActionType.ClipboardGet or EditorActionType.ClipboardSet => "Unknown Action",
                _ => "Unknown Action",
            };
        }

        private static string GetTextInputDisplayName(EditorAction action)
        {
            if (string.IsNullOrEmpty(action.Text))
            {
                return "Text Input (empty)";
            }

            var truncated = action.Text.Length > 25 ? action.Text[..25] + "..." : action.Text;
            return $"Type \"{truncated}\"";
        }

        private static string GetSetVariableDisplayName(EditorAction action)
        {
            if (action.UseLegacyScriptTextDisplay)
            {
                return $"Set {action.Text}";
            }

            return EditorActionScriptTokens.IsValidVariableName(action.ScriptVariableName) ? $"Set {action.ScriptVariableName} = {action.BuildSetValueToken()}" : "Set Variable";
        }

        private static string GetMousePositionDisplayName(EditorAction action)
        {
            return EditorActionScriptTokens.IsValidVariableName(action.MousePositionXVariableName) && EditorActionScriptTokens.IsValidVariableName(action.MousePositionYVariableName) ? $"Capture mouse position into {action.MousePositionXVariableName}, {action.MousePositionYVariableName}" : "Capture Mouse Position";
        }

        private static string GetIncrementVariableDisplayName(EditorAction action)
        {
            if (action.UseLegacyScriptTextDisplay)
            {
                return $"Inc {action.Text}";
            }

            return EditorActionScriptTokens.IsValidVariableName(action.ScriptVariableName) ? $"Inc {action.ScriptVariableName} by {BuildNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue)}" : "Increment Variable";
        }

        private static string GetDecrementVariableDisplayName(EditorAction action)
        {
            if (action.UseLegacyScriptTextDisplay)
            {
                return $"Dec {action.Text}";
            }

            return EditorActionScriptTokens.IsValidVariableName(action.ScriptVariableName) ? $"Dec {action.ScriptVariableName} by {BuildNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue)}" : "Decrement Variable";
        }

        private static string GetMultiplyVariableDisplayName(EditorAction action)
        {
            if (action.UseLegacyScriptTextDisplay)
            {
                return $"Mul {action.Text}";
            }

            return EditorActionScriptTokens.IsValidVariableName(action.ScriptVariableName) ? $"Mul {action.ScriptVariableName} by {BuildNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue)}" : "Multiply Variable";
        }

        private static string GetDivideVariableDisplayName(EditorAction action)
        {
            if (action.UseLegacyScriptTextDisplay)
            {
                return $"Div {action.Text}";
            }

            return EditorActionScriptTokens.IsValidVariableName(action.ScriptVariableName) ? $"Div {action.ScriptVariableName} by {BuildNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue)}" : "Divide Variable";
        }

        private static string GetShellCommandDisplayName(EditorAction action)
        {
            if (string.IsNullOrWhiteSpace(action.ShellCommand))
            {
                return "Shell Command";
            }

            var commandText = action.ShellCommand.Length > 30 ? action.ShellCommand[..30] + "..." : action.ShellCommand;
            return $"Shell {action.ShellCommandMode}: \"{commandText}\"";
        }

        private static string GetRawScriptStepDisplayName(EditorAction action)
        {
            if (string.IsNullOrWhiteSpace(action.Text))
            {
                return "Raw Script Step";
            }

            var stepText = action.Text.Length > 40 ? action.Text[..40] + "..." : action.Text;
            return $"Raw Script: {stepText}";
        }

        private static string FormatRelativeCoordinateToken(string token)
        {
            return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value.ToString("+#;-#;0", CultureInfo.InvariantCulture) : token;
        }

        private static string BuildPixelColorDisplayName(EditorActionScreenReadingPayload payload)
        {
            return payload.IsAbsolute ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Pixel color ({payload.ScreenX}, {payload.ScreenY}) -> {payload.ScreenColorVariableName}") : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Pixel color rel ({payload.ScreenX:+#;-#;0}, {payload.ScreenY:+#;-#;0}) -> {payload.ScreenColorVariableName}");
        }

        private static string BuildWaitColorDisplayName(EditorActionScreenReadingPayload payload)
        {
            return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Wait color {payload.FormatTargetColorToken()} at ({payload.ScreenX}, {payload.ScreenY}) -> {payload.ScreenColorVariableName}");
        }

        private static string BuildPixelSearchDisplayName(EditorActionScreenReadingPayload payload)
        {
            return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Pixel search {payload.FormatTargetColorToken()} in ({payload.ScreenLeft}, {payload.ScreenTop}, {payload.ScreenWidth}x{payload.ScreenHeight}) -> {payload.ScreenFoundVariableName}, {payload.ScreenFoundXVariableName}, {payload.ScreenFoundYVariableName}");
        }

        private static string BuildImageSearchDisplayName(EditorAction action)
        {
            var imageName = string.IsNullOrWhiteSpace(action.ImageAssetName) ? "image required" : action.ImageAssetName;
            return $"Image search {imageName} in ({action.ImageSearchRegionLeftToken}, {action.ImageSearchRegionTopToken}, {action.ImageSearchRegionWidthToken}x{action.ImageSearchRegionHeightToken}) -> {action.ScreenFoundVariableName}, {action.ScreenFoundXVariableName}, {action.ScreenFoundYVariableName}";
        }

        private static string BuildImageClickDisplayName(EditorAction action)
        {
            var imageName = string.IsNullOrWhiteSpace(action.ImageAssetName) ? "image required" : action.ImageAssetName;
            return $"Image click {imageName} in ({action.ImageSearchRegionLeftToken}, {action.ImageSearchRegionTopToken}, {action.ImageSearchRegionWidthToken}x{action.ImageSearchRegionHeightToken})";
        }

        private static string BuildWaitImageDisplayName(EditorAction action)
        {
            var imageName = string.IsNullOrWhiteSpace(action.ImageAssetName) ? "image required" : action.ImageAssetName;
            return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Wait image {imageName} ({action.ScreenTimeoutMs}ms) -> {action.ScreenFoundVariableName}, {action.ScreenFoundXVariableName}, {action.ScreenFoundYVariableName}");
        }

        private static string BuildScreenshotDisplayName(EditorAction action)
        {
            string destination;
            if (action.ScreenshotCopyToClipboard)
            {
                destination = string.IsNullOrWhiteSpace(action.ScreenshotOutputPath) ? "clipboard" : $"{action.ScreenshotOutputPath} + clipboard";
            }
            else
            {
                destination = string.IsNullOrWhiteSpace(action.ScreenshotOutputPath) ? "destination required" : action.ScreenshotOutputPath;
            }

            return action.ScreenshotUseRegion ? $"Screenshot ({action.ScreenshotRegionX}, {action.ScreenshotRegionY}, {action.ScreenshotRegionWidth}x{action.ScreenshotRegionHeight}) -> {destination}" : $"Screenshot -> {destination}";
        }

        private static string BuildWindowCommandDisplayName(EditorAction action)
        {
            return action.WindowCommandMode switch
            {
                WindowCommandMode.Active => $"Get active window {action.WindowActiveField} -> {action.WindowOutputVariable}",
                WindowCommandMode.Search => $"Search window by {action.WindowSelectorKind} \"{action.WindowSelectorValue}\" -> {action.WindowOutputVariable}",
                WindowCommandMode.Wait => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Wait for window {action.WindowSelectorKind} \"{action.WindowSelectorValue}\" ({action.WindowTimeoutMs}ms) -> {action.WindowOutputVariable}"),
                WindowCommandMode.Focus => action.FormatWindowSelectorSummary("Focus"),
                WindowCommandMode.Close => action.FormatWindowSelectorSummary("Close"),
                WindowCommandMode.Move => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Move active window to {action.WindowX}, {action.WindowY}"),
                WindowCommandMode.Resize => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Resize active window to {action.WindowWidth}x{action.WindowHeight}"),
                WindowCommandMode.Center => "Center active window",
                WindowCommandMode.Maximize => "Maximize active window",
                WindowCommandMode.Fullscreen => "Fullscreen active window",
                WindowCommandMode.Floating => "Float active window",
                WindowCommandMode.WorkspaceGet => $"Get active workspace -> {action.WindowOutputVariable}",
                WindowCommandMode.WorkspaceSwitch => $"Switch to workspace {action.WindowWorkspace}",
                WindowCommandMode.WorkspaceMoveActive => $"Move active window to workspace {action.WindowWorkspace}",
                WindowCommandMode.WorkspaceMoveWindow => $"Move window {action.WindowSelectorValue} to workspace {action.WindowWorkspace}",
                _ => "Window Command",
            };
        }
    }
}
