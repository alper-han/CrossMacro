using System;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.UI.Localization;

public sealed class EditorActionDisplayFormatter(ILocalizationService localizationService)
{
    public string Format(EditorAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return action.Type switch
        {
            EditorActionType.MouseMove when action.IsAbsolute => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseMoveAbsolute"], action.X, action.Y),
            EditorActionType.MouseMove => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseMoveRelative"], action.X, action.Y),
            EditorActionType.MouseClick when action.UseCurrentPosition => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseClickCurrent"], FormatMouseButton(action.Button)),
            EditorActionType.MouseClick when action.IsAbsolute => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseClickAbsolute"], FormatMouseButton(action.Button), action.X, action.Y),
            EditorActionType.MouseClick => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseClickRelative"], FormatMouseButton(action.Button), action.X, action.Y),
            EditorActionType.MouseDown when action.UseCurrentPosition => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseDownCurrent"], FormatMouseButton(action.Button)),
            EditorActionType.MouseDown when action.IsAbsolute => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseDownAbsolute"], FormatMouseButton(action.Button), action.X, action.Y),
            EditorActionType.MouseDown => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseDownRelative"], FormatMouseButton(action.Button), action.X, action.Y),
            EditorActionType.MouseUp when action.UseCurrentPosition => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseUpCurrent"], FormatMouseButton(action.Button)),
            EditorActionType.MouseUp when action.IsAbsolute => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseUpAbsolute"], FormatMouseButton(action.Button), action.X, action.Y),
            EditorActionType.MouseUp => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_MouseUpRelative"], FormatMouseButton(action.Button), action.X, action.Y),
            EditorActionType.KeyPress => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_KeyPress"], action.KeyName ?? action.KeyCode.ToString(localizationService.CurrentCulture)),
            EditorActionType.KeyDown => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_KeyDown"], action.KeyName ?? action.KeyCode.ToString(localizationService.CurrentCulture)),
            EditorActionType.KeyUp => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_KeyUp"], action.KeyName ?? action.KeyCode.ToString(localizationService.CurrentCulture)),
            EditorActionType.Delay when action.UseRandomDelay => string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_DelayRandom"], action.RandomDelayMinMs, action.RandomDelayMaxMs),
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
            EditorActionType.RepeatBlockStart => localizationService["Editor_Action_RepeatBlockShort"],
            EditorActionType.IfBlockStart => localizationService["Editor_Action_IfBlockShort"],
            EditorActionType.ElseBlockStart => localizationService["Editor_Action_ElseBlockShort"],
            EditorActionType.WhileBlockStart => localizationService["Editor_Action_WhileBlockShort"],
            EditorActionType.ForBlockStart => localizationService["Editor_Action_ForBlockShort"],
            EditorActionType.PixelColor => FormatPixelColor(GetScreenReadingPayload(action)),
            EditorActionType.WaitColor => FormatWaitColor(GetScreenReadingPayload(action)),
            EditorActionType.PixelSearch => FormatPixelSearch(GetScreenReadingPayload(action)),
            EditorActionType.Break => localizationService["Editor_Action_BreakShort"],
            EditorActionType.Continue => localizationService["Editor_Action_ContinueShort"],
            EditorActionType.BlockEnd => localizationService["Editor_Action_EndBlockShort"],
            EditorActionType.RawScriptStep => string.IsNullOrWhiteSpace(action.Text)
                ? localizationService["Editor_Action_RawScriptStepShort"]
                : string.Format(localizationService.CurrentCulture, localizationService["Editor_Action_RawScriptStep"], Truncate(action.Text, 40)),
            _ => localizationService["Editor_Action_UnknownShort"]
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
            EditorActionType.RawScriptStep => localizationService["Editor_ActionType_RawScriptStep"],
            _ => actionType.ToString()
        };
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
            _ => localizationService["Editor_BlockName_Block"]
        };
    }

    private string FormatMouseButton(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => localizationService["MouseButton_Left"],
            MouseButton.Right => localizationService["MouseButton_Right"],
            MouseButton.Middle => localizationService["MouseButton_Middle"],
            MouseButton.ScrollUp => localizationService["MouseButton_ScrollUp"],
            MouseButton.ScrollDown => localizationService["MouseButton_ScrollDown"],
            MouseButton.ScrollLeft => localizationService["MouseButton_ScrollLeft"],
            MouseButton.ScrollRight => localizationService["MouseButton_ScrollRight"],
            _ => button.ToString()
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
