namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Classifies selected-action changes without coupling presentation invalidation to undo state.</summary>
internal readonly record struct EditorPresentationChanges(
    bool NormalizeAction,
    bool Visibility,
    bool ScreenReading,
    bool VariableNames,
    bool ImagePreview,
    bool TextInput,
    bool KeyName,
    bool Coordinates)
{
    internal static EditorPresentationChanges For(string? propertyName) => new(
        NormalizeAction: propertyName is nameof(EditorAction.Type)
            or nameof(EditorAction.UseRandomDelay)
            or nameof(EditorAction.UseCurrentPosition)
            or nameof(EditorAction.ShellCommandMode)
            or nameof(EditorAction.WindowCommandMode)
            or nameof(EditorAction.WindowSelectorKind)
            or nameof(EditorAction.ScriptLeftOperandType)
            or nameof(EditorAction.ScriptRightOperandType)
            or nameof(EditorAction.ScriptLeftOperand)
            or nameof(EditorAction.ScriptRightOperand),
        Visibility: propertyName is nameof(EditorAction.Type)
            or nameof(EditorAction.UseRandomDelay)
            or nameof(EditorAction.UseCurrentPosition)
            or nameof(EditorAction.ScreenTargetColorSource)
            or nameof(EditorAction.ScreenshotUseRegion)
            or nameof(EditorAction.ForHasStep)
            or nameof(EditorAction.WindowCommandMode)
            or nameof(EditorAction.WindowSelectorKind)
            or nameof(EditorAction.ScriptValueType)
            or nameof(EditorAction.ShellCommandMode)
            or nameof(EditorAction.ScriptNumericSourceType)
            or nameof(EditorAction.ScriptLeftOperandType)
            or nameof(EditorAction.ScriptRightOperandType)
            or nameof(EditorAction.ScriptLeftOperand)
            or nameof(EditorAction.ScriptRightOperand)
            or nameof(EditorAction.ForStartType)
            or nameof(EditorAction.ForEndType)
            or nameof(EditorAction.ForStepType),
        ScreenReading: propertyName is nameof(EditorAction.ScreenColorHex)
            or nameof(EditorAction.ScreenTargetColorSource)
            or nameof(EditorAction.ScreenTargetColorVariableName),
        VariableNames: propertyName is nameof(EditorAction.Type)
            or nameof(EditorAction.ImageAssetName)
            or nameof(EditorAction.Text)
            or nameof(EditorAction.ScriptVariableName)
            or nameof(EditorAction.ForVariableName)
            or nameof(EditorAction.ScriptValue)
            or nameof(EditorAction.ScriptNumericValue)
            or nameof(EditorAction.ScriptLeftOperand)
            or nameof(EditorAction.ScriptRightOperand)
            or nameof(EditorAction.ForStartValue)
            or nameof(EditorAction.ForEndValue)
            or nameof(EditorAction.ForStepValue)
            or nameof(EditorAction.MousePositionXVariableName)
            or nameof(EditorAction.MousePositionYVariableName)
            or nameof(EditorAction.ScreenColorVariableName)
            or nameof(EditorAction.ScreenFoundVariableName)
            or nameof(EditorAction.ScreenFoundXVariableName)
            or nameof(EditorAction.ScreenFoundYVariableName)
            or nameof(EditorAction.ShellExitCodeVariableName)
            or nameof(EditorAction.ShellStandardOutputVariableName)
            or nameof(EditorAction.ShellStandardErrorVariableName)
            or nameof(EditorAction.WindowOutputVariable),
        ImagePreview: propertyName is nameof(EditorAction.Type)
            or nameof(EditorAction.ImageAssetName),
        TextInput: propertyName is nameof(EditorAction.Type)
            or nameof(EditorAction.Text),
        KeyName: propertyName is nameof(EditorAction.KeyCode),
        Coordinates: propertyName is nameof(EditorAction.IsAbsolute)
            or nameof(EditorAction.CoordinateSpace));
}
