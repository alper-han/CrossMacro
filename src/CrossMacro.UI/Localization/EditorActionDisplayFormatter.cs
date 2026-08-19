
namespace CrossMacro.UI.Localization;

public sealed class EditorActionDisplayFormatter(ILocalizationService localizationService)
{
    private readonly ILocalizationService localizationService = localizationService;

    public string Format(EditorAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return action.Type switch
        {
            EditorActionType.MouseMove when action.IsAbsolute => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseMoveAbsolute"], FormatCoordinate(action.CoordinateXToken), FormatCoordinate(action.CoordinateYToken)),
            EditorActionType.MouseMove => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseMoveRelative"], FormatCoordinate(action.CoordinateXToken), FormatCoordinate(action.CoordinateYToken)),
            EditorActionType.MouseClick when action.UseCurrentPosition => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseClickCurrent"], FormatMouseButton(action.Button)),
            EditorActionType.MouseClick when action.IsAbsolute => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseClickAbsolute"], FormatMouseButton(action.Button), FormatCoordinate(action.CoordinateXToken), FormatCoordinate(action.CoordinateYToken)),
            EditorActionType.MouseClick => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseClickRelative"], FormatMouseButton(action.Button), FormatCoordinate(action.CoordinateXToken), FormatCoordinate(action.CoordinateYToken)),
            EditorActionType.MouseDown when action.UseCurrentPosition => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseDownCurrent"], FormatMouseButton(action.Button)),
            EditorActionType.MouseDown when action.IsAbsolute => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseDownAbsolute"], FormatMouseButton(action.Button), FormatCoordinate(action.CoordinateXToken), FormatCoordinate(action.CoordinateYToken)),
            EditorActionType.MouseDown => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseDownRelative"], FormatMouseButton(action.Button), FormatCoordinate(action.CoordinateXToken), FormatCoordinate(action.CoordinateYToken)),
            EditorActionType.MouseUp when action.UseCurrentPosition => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseUpCurrent"], FormatMouseButton(action.Button)),
            EditorActionType.MouseUp when action.IsAbsolute => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseUpAbsolute"], FormatMouseButton(action.Button), FormatCoordinate(action.CoordinateXToken), FormatCoordinate(action.CoordinateYToken)),
            EditorActionType.MouseUp => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseUpRelative"], FormatMouseButton(action.Button), FormatCoordinate(action.CoordinateXToken), FormatCoordinate(action.CoordinateYToken)),
            EditorActionType.KeyPress => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_KeyPress"], action.KeyName ?? action.KeyCode.ToString(localizationService.CurrentCulture)),
            EditorActionType.KeyDown => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_KeyDown"], action.KeyName ?? action.KeyCode.ToString(localizationService.CurrentCulture)),
            EditorActionType.KeyUp => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_KeyUp"], action.KeyName ?? action.KeyCode.ToString(localizationService.CurrentCulture)),
            EditorActionType.Delay when action.UseRandomDelay => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_DelayRandom"], action.RandomDelayMinMs, action.RandomDelayMaxMs),
            EditorActionType.Delay when action.DelayMicroseconds % MacroTiming.MicrosecondsPerMillisecond is not 0 => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_DelayPrecise"], action.DelayDuration),
            EditorActionType.Delay => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_Delay"], action.DelayMs),
            EditorActionType.ScrollVertical when action.ScrollAmount > 0 => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_ScrollUp"], action.ScrollAmount),
            EditorActionType.ScrollVertical => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_ScrollDown"], Math.Abs(action.ScrollAmount)),
            EditorActionType.ScrollHorizontal when action.ScrollAmount > 0 => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_ScrollRight"], action.ScrollAmount),
            EditorActionType.ScrollHorizontal => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_ScrollLeft"], Math.Abs(action.ScrollAmount)),
            EditorActionType.TextInput => string.IsNullOrEmpty(action.Text)
                ? localizationService["Editor_Action_TextInputEmpty"]
                : string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_TextInput"], Truncate(TextInputControlCharacterFormatter.Escape(action.Text), 25)),
            EditorActionType.SetVariable => localizationService["Editor_Action_SetVariableShort"],
            EditorActionType.IncrementVariable => localizationService["Editor_Action_IncrementVariableShort"],
            EditorActionType.DecrementVariable => localizationService["Editor_Action_DecrementVariableShort"],
            EditorActionType.MultiplyVariable => localizationService["Editor_Action_MultiplyVariableShort"],
            EditorActionType.DivideVariable => localizationService["Editor_Action_DivideVariableShort"],
            EditorActionType.RepeatBlockStart => localizationService["Editor_Action_RepeatBlockShort"],
            EditorActionType.IfBlockStart => localizationService["Editor_Action_IfBlockShort"],
            EditorActionType.ElseBlockStart => localizationService["Editor_Action_ElseBlockShort"],
            EditorActionType.WhileBlockStart => localizationService["Editor_Action_WhileBlockShort"],
            EditorActionType.ForBlockStart => localizationService["Editor_Action_ForBlockShort"],
            EditorActionType.PixelColor => FormatPixelColor(GetScreenReadingPayload(action)),
            EditorActionType.WaitColor => FormatWaitColor(GetScreenReadingPayload(action)),
            EditorActionType.PixelSearch => FormatPixelSearch(GetScreenReadingPayload(action)),
            EditorActionType.ImageSearch => FormatImageSearch(action),
            EditorActionType.ImageClick => FormatImageClick(action),
            EditorActionType.WaitImage => FormatWaitImage(action),
            EditorActionType.MousePosition => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MousePosition"], action.MousePositionXVariableName, action.MousePositionYVariableName),
            EditorActionType.ClipboardGet => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_ClipboardGet"], action.ScriptVariableName),
            EditorActionType.ClipboardSet => string.IsNullOrEmpty(action.Text)
                ? localizationService["Editor_Action_ClipboardSetEmpty"]
                : string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_ClipboardSet"], Truncate(TextInputControlCharacterFormatter.Escape(action.Text), 25)),
            EditorActionType.ShellCommand => FormatShellCommand(action),
            EditorActionType.Screenshot => FormatScreenshot(action),
            EditorActionType.WindowCommand => FormatWindowCommand(action),
            EditorActionType.Break => localizationService["Editor_Action_BreakShort"],
            EditorActionType.Continue => localizationService["Editor_Action_ContinueShort"],
            EditorActionType.BlockEnd => localizationService["Editor_Action_EndBlockShort"],
            EditorActionType.RawScriptStep => string.IsNullOrWhiteSpace(action.Text)
                ? localizationService["Editor_Action_RawScriptStepShort"]
                : string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_RawScriptStep"], Truncate(action.Text, 40)),
            _ => localizationService["Editor_Action_UnknownShort"],
        };
    }

    public string FormatActionType(EditorActionType actionType)
    {
        return actionType switch
        {
            EditorActionType.MouseMove => localizationService["Editor_ActionType_MouseMove"],
            EditorActionType.MouseClick => localizationService["Editor_ActionType_MouseClick"],
            EditorActionType.MouseDown => localizationService["Editor_ActionType_MouseDown"],
            EditorActionType.MouseUp => localizationService["Editor_ActionType_MouseUp"],
            EditorActionType.KeyPress => localizationService["Editor_ActionType_KeyPress"],
            EditorActionType.KeyDown => localizationService["Editor_ActionType_KeyDown"],
            EditorActionType.KeyUp => localizationService["Editor_ActionType_KeyUp"],
            EditorActionType.Delay => localizationService["Editor_ActionType_Delay"],
            EditorActionType.ScrollVertical => localizationService["Editor_ActionType_ScrollVertical"],
            EditorActionType.ScrollHorizontal => localizationService["Editor_ActionType_ScrollHorizontal"],
            EditorActionType.TextInput => localizationService["Editor_ActionType_TextInput"],
            EditorActionType.SetVariable => localizationService["Editor_ActionType_SetVariable"],
            EditorActionType.IncrementVariable => localizationService["Editor_ActionType_IncrementVariable"],
            EditorActionType.DecrementVariable => localizationService["Editor_ActionType_DecrementVariable"],
            EditorActionType.MultiplyVariable => localizationService["Editor_ActionType_MultiplyVariable"],
            EditorActionType.DivideVariable => localizationService["Editor_ActionType_DivideVariable"],
            EditorActionType.RepeatBlockStart => localizationService["Editor_ActionType_RepeatBlockStart"],
            EditorActionType.IfBlockStart => localizationService["Editor_ActionType_IfBlockStart"],
            EditorActionType.ElseBlockStart => localizationService["Editor_ActionType_ElseBlockStart"],
            EditorActionType.WhileBlockStart => localizationService["Editor_ActionType_WhileBlockStart"],
            EditorActionType.ForBlockStart => localizationService["Editor_ActionType_ForBlockStart"],
            EditorActionType.BlockEnd => localizationService["Editor_ActionType_BlockEnd"],
            EditorActionType.Break => localizationService["Editor_ActionType_Break"],
            EditorActionType.Continue => localizationService["Editor_ActionType_Continue"],
            EditorActionType.PixelColor => localizationService["Editor_ActionType_PixelColor"],
            EditorActionType.WaitColor => localizationService["Editor_ActionType_WaitColor"],
            EditorActionType.PixelSearch => localizationService["Editor_ActionType_PixelSearch"],
            EditorActionType.ImageSearch => localizationService["Editor_ActionType_ImageSearch"],
            EditorActionType.ImageClick => localizationService["Editor_ActionType_ImageClick"],
            EditorActionType.WaitImage => localizationService["Editor_ActionType_WaitImage"],
            EditorActionType.MousePosition => localizationService["Editor_ActionType_MousePosition"],
            EditorActionType.ClipboardGet => localizationService["Editor_ActionType_ClipboardGet"],
            EditorActionType.ClipboardSet => localizationService["Editor_ActionType_ClipboardSet"],
            EditorActionType.ShellCommand => localizationService["Editor_ActionType_ShellCommand"],
            EditorActionType.Screenshot => localizationService["Editor_ActionType_Screenshot"],
            EditorActionType.WindowCommand => localizationService["Editor_ActionType_WindowCommand"],
            EditorActionType.RawScriptStep => localizationService["Editor_ActionType_RawScriptStep"],
            _ => actionType.ToString(),
        };
    }

    private static object FormatCoordinate(string token)
    {
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : token;
    }

    public string FormatBlockName(EditorActionType actionType)
    {
        return actionType switch
        {
            EditorActionType.IfBlockStart => localizationService["Editor_BlockName_If"],
            EditorActionType.ElseBlockStart => localizationService["Editor_BlockName_Else"],
            EditorActionType.WhileBlockStart => localizationService["Editor_BlockName_While"],
            EditorActionType.ForBlockStart => localizationService["Editor_BlockName_For"],
            EditorActionType.RepeatBlockStart => localizationService["Editor_BlockName_Repeat"],
            EditorActionType.MouseMove => localizationService["Editor_BlockName_Block"],
            EditorActionType.MouseClick => localizationService["Editor_BlockName_Block"],
            EditorActionType.MouseDown => localizationService["Editor_BlockName_Block"],
            EditorActionType.MouseUp => localizationService["Editor_BlockName_Block"],
            EditorActionType.KeyPress => localizationService["Editor_BlockName_Block"],
            EditorActionType.KeyDown => localizationService["Editor_BlockName_Block"],
            EditorActionType.KeyUp => localizationService["Editor_BlockName_Block"],
            EditorActionType.Delay => localizationService["Editor_BlockName_Block"],
            EditorActionType.ScrollVertical => localizationService["Editor_BlockName_Block"],
            EditorActionType.ScrollHorizontal => localizationService["Editor_BlockName_Block"],
            EditorActionType.TextInput => localizationService["Editor_BlockName_Block"],
            EditorActionType.SetVariable => localizationService["Editor_BlockName_Block"],
            EditorActionType.IncrementVariable => localizationService["Editor_BlockName_Block"],
            EditorActionType.DecrementVariable => localizationService["Editor_BlockName_Block"],
            EditorActionType.MultiplyVariable => localizationService["Editor_BlockName_Block"],
            EditorActionType.DivideVariable => localizationService["Editor_BlockName_Block"],
            EditorActionType.BlockEnd => localizationService["Editor_BlockName_Block"],
            EditorActionType.Break => localizationService["Editor_BlockName_Block"],
            EditorActionType.Continue => localizationService["Editor_BlockName_Block"],
            EditorActionType.PixelColor => localizationService["Editor_BlockName_Block"],
            EditorActionType.WaitColor => localizationService["Editor_BlockName_Block"],
            EditorActionType.PixelSearch => localizationService["Editor_BlockName_Block"],
            EditorActionType.ImageSearch => localizationService["Editor_BlockName_Block"],
            EditorActionType.ImageClick => localizationService["Editor_BlockName_Block"],
            EditorActionType.WaitImage => localizationService["Editor_BlockName_Block"],
            EditorActionType.MousePosition => localizationService["Editor_BlockName_Block"],
            EditorActionType.ClipboardGet => localizationService["Editor_BlockName_Block"],
            EditorActionType.ClipboardSet => localizationService["Editor_BlockName_Block"],
            EditorActionType.ShellCommand => localizationService["Editor_BlockName_Block"],
            EditorActionType.Screenshot => localizationService["Editor_BlockName_Block"],
            EditorActionType.WindowCommand => localizationService["Editor_BlockName_Block"],
            EditorActionType.RawScriptStep => localizationService["Editor_BlockName_Block"],
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, message: null),
        };
    }

    private string FormatMouseButton(MacroMouseButton button)
    {
        return button switch
        {
            MacroMouseButton.Left => localizationService["MouseButton_Left"],
            MacroMouseButton.Right => localizationService["MouseButton_Right"],
            MacroMouseButton.Middle => localizationService["MouseButton_Middle"],
            MacroMouseButton.ScrollUp => localizationService["MouseButton_ScrollUp"],
            MacroMouseButton.ScrollDown => localizationService["MouseButton_ScrollDown"],
            MacroMouseButton.ScrollLeft => localizationService["MouseButton_ScrollLeft"],
            MacroMouseButton.ScrollRight => localizationService["MouseButton_ScrollRight"],
            MacroMouseButton.None => button.ToString(),
            MacroMouseButton.Side1 => button.ToString(),
            MacroMouseButton.Side2 => button.ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, message: null),
        };
    }

    private string FormatPixelColor(EditorActionScreenReadingPayload payload)
    {
        var key = payload.IsAbsolute ? "Editor_Action_PixelColorAbsolute" : "Editor_Action_PixelColorRelative";
        return string.Format(
            localizationService.CurrentCulture,
            localizationService[key],
            payload.ScreenX,
            payload.ScreenY,
            payload.ScreenColorVariableName);
    }

    private string FormatWaitColor(EditorActionScreenReadingPayload payload)
    {
        return string.Format(
            localizationService.CurrentCulture,
            localizationService["Editor_Action_WaitColor"],
            payload.FormatTargetColorToken(),
            payload.ScreenX,
            payload.ScreenY,
            payload.ScreenTimeoutMs,
            payload.ScreenColorVariableName);
    }

    private string FormatPixelSearch(EditorActionScreenReadingPayload payload)
    {
        return string.Format(
            localizationService.CurrentCulture,
            localizationService["Editor_Action_PixelSearch"],
            payload.FormatTargetColorToken(),
            payload.ScreenLeft,
            payload.ScreenTop,
            payload.ScreenWidth,
            payload.ScreenHeight,
            payload.ScreenFoundVariableName,
            payload.ScreenFoundXVariableName,
            payload.ScreenFoundYVariableName,
            payload.ScreenTolerance);
    }

    private string FormatImageSearch(EditorAction action)
    {
        return string.Format(
            localizationService.CurrentCulture,
            localizationService["Editor_Action_ImageSearch"],
            action.ImageAssetName,
            action.ScreenLeft,
            action.ScreenTop,
            action.ScreenWidth,
            action.ScreenHeight,
            action.ScreenFoundVariableName,
            action.ScreenFoundXVariableName,
            action.ScreenFoundYVariableName,
            action.ImageSearchSimilarity);
    }

    private string FormatImageClick(EditorAction action)
    {
        return string.Format(
            localizationService.CurrentCulture,
            localizationService["Editor_Action_ImageClick"],
            action.ImageAssetName,
            action.ScreenLeft,
            action.ScreenTop,
            action.ScreenWidth,
            action.ScreenHeight);
    }

    private string FormatWaitImage(EditorAction action)
    {
        return string.Format(
            localizationService.CurrentCulture,
            localizationService["Editor_Action_WaitImage"],
            action.ImageAssetName,
            action.ScreenLeft,
            action.ScreenTop,
            action.ScreenWidth,
            action.ScreenHeight,
            action.ScreenTimeoutMs);
    }

    private string FormatShellCommand(EditorAction action)
    {
        var command = string.IsNullOrWhiteSpace(action.ShellCommand)
            ? localizationService["Editor_Action_ShellCommandEmpty"]
            : Truncate(TextInputControlCharacterFormatter.Escape(action.ShellCommand), 32);
        return action.ShellCommandMode switch
        {
            ShellCommandMode.ShellCapture => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_ShellCapture"], command, action.ShellExitCodeVariableName, action.ShellStandardOutputVariableName, action.ShellStandardErrorVariableName),
            ShellCommandMode.ShellInput => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_ShellInput"], command),
            ShellCommandMode.ShellCaptureInput => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_ShellCaptureInput"], command, action.ShellExitCodeVariableName, action.ShellStandardOutputVariableName, action.ShellStandardErrorVariableName),
            ShellCommandMode.Shell => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_ShellCommand"], command),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action.ShellCommandMode, message: null),
        };
    }

    private string FormatScreenshot(EditorAction action)
    {
        var destination = FormatScreenshotDestination(action);
        return action.ScreenshotUseRegion
            ? string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_ScreenshotRegion"], action.ScreenshotRegionX, action.ScreenshotRegionY, action.ScreenshotRegionWidth, action.ScreenshotRegionHeight, destination)
            : string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_Screenshot"], destination);
    }

    private string FormatScreenshotDestination(EditorAction action)
    {
        if (action.ScreenshotCopyToClipboard)
        {
            return string.IsNullOrWhiteSpace(action.ScreenshotOutputPath)
                ? localizationService["Editor_ScreenshotClipboardDestination"]
                : string.Format(localizationService.CurrentCulture, localizationService["Editor_ScreenshotFileAndClipboardDestination"], action.ScreenshotOutputPath);
        }

        return string.IsNullOrWhiteSpace(action.ScreenshotOutputPath)
            ? localizationService["Editor_ScreenshotDestinationRequired"]
            : action.ScreenshotOutputPath;
    }

    private string FormatWindowCommand(EditorAction action)
    {
        return action.WindowCommandMode switch
        {
            WindowCommandMode.Active => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_WindowActive"], action.WindowActiveField, action.WindowOutputVariable),
            WindowCommandMode.Search => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_WindowSearch"], action.WindowSelectorKind, action.WindowSelectorValue, action.WindowOutputVariable),
            WindowCommandMode.Wait => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_WindowWait"], action.WindowSelectorKind, action.WindowSelectorValue, action.WindowTimeoutMs, action.WindowOutputVariable),
            WindowCommandMode.Focus => FormatWindowSelectorAction(action, "Editor_Action_WindowFocus", "Editor_Action_WindowFocusActive"),
            WindowCommandMode.Close => FormatWindowSelectorAction(action, "Editor_Action_WindowClose", "Editor_Action_WindowCloseActive"),
            WindowCommandMode.Move => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_WindowMove"], action.WindowX, action.WindowY),
            WindowCommandMode.Resize => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_WindowResize"], action.WindowWidth, action.WindowHeight),
            WindowCommandMode.Center => localizationService["Editor_Action_WindowCenter"],
            WindowCommandMode.Maximize => localizationService["Editor_Action_WindowMaximize"],
            WindowCommandMode.Fullscreen => localizationService["Editor_Action_WindowFullscreen"],
            WindowCommandMode.Floating => localizationService["Editor_Action_WindowFloat"],
            WindowCommandMode.WorkspaceGet => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_WindowWorkspaceGet"], action.WindowOutputVariable),
            WindowCommandMode.WorkspaceSwitch => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_WindowWorkspaceSwitch"], action.WindowWorkspace),
            WindowCommandMode.WorkspaceMoveActive => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_WindowWorkspaceMoveActive"], action.WindowWorkspace),
            WindowCommandMode.WorkspaceMoveWindow => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_WindowWorkspaceMoveWindow"], action.WindowSelectorValue, action.WindowWorkspace),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action.WindowCommandMode, message: null),
        };
    }

    private string FormatWindowSelectorAction(EditorAction action, string selectorKey, string activeKey)
    {
        return action.WindowSelectorKind is "active"
            ? localizationService[activeKey]
            : string.Format(localizationService.CurrentCulture, localizationService[selectorKey], action.WindowSelectorKind, action.WindowSelectorValue);
    }

    private static EditorActionScreenReadingPayload GetScreenReadingPayload(EditorAction action)
    {
        if (!action.TryGetScreenReadingPayload(out var payload))
        {
            throw new InvalidOperationException("Action type does not contain a screen-reading payload.");
        }

        return payload;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length > maxLength ? value[..maxLength] + "..." : value;
    }
}
