namespace CrossMacro.Core.Models.Editing;

public partial class EditorAction
{
    // Mutable binding state stays private to its action family and is copied as one unit.
    private sealed class InputState
    {
        public int X;
        public int Y;
        public string? CoordinateXToken;
        public string? CoordinateYToken;
        public bool IsAbsolute = true;
        public MouseCoordinateSpace CoordinateSpace = MouseCoordinateSpace.RawDevice;
        public MacroMouseButton Button = MacroMouseButton.Left;
        public int KeyCode;
        public long DelayMicroseconds;
        public bool UseRandomDelay;
        public int RandomDelayMinMs;
        public int RandomDelayMaxMs;
        public bool UseCurrentPosition;
        public int ScrollAmount = 1;
        public string? KeyName;
        public string Text = string.Empty;
        public List<MacroEvent>? PreservedTextInputEvents;
        public string? PreservedTextInputText;

        public InputState Copy()
        {
            var copy = (InputState)MemberwiseClone();
            copy.PreservedTextInputEvents = PreservedTextInputEvents?.ToList();
            return copy;
        }
    }

    // Mutable binding state stays private to its action family and is copied as one unit.
    private sealed class ScriptState
    {
        public bool PreferLegacyScriptText;
        public string ScriptVariableName = "i";
        public ClipboardCopyShortcut ClipboardCopyShortcut = ClipboardCopyShortcut.CtrlC;
        public string MousePositionXVariableName = "mouse_x";
        public string MousePositionYVariableName = "mouse_y";
        public ScriptValueType ScriptValueType = ScriptValueType.Number;
        public string ScriptValue = "0";
        public ScriptNumericSourceType ScriptNumericSourceType = ScriptNumericSourceType.Number;
        public string ScriptNumericValue = "1";
        public ScriptOperandType ScriptLeftOperandType = ScriptOperandType.VariableReference;
        public string ScriptLeftOperand = "i";
        public ScriptConditionOperator ScriptConditionOperator = ScriptConditionOperator.LessThan;
        public ScriptOperandType ScriptRightOperandType = ScriptOperandType.Number;
        public string ScriptRightOperand = "10";
        public string ForVariableName = "i";
        public ScriptNumericSourceType ForStartType = ScriptNumericSourceType.Number;
        public string ForStartValue = "0";
        public ScriptNumericSourceType ForEndType = ScriptNumericSourceType.Number;
        public string ForEndValue = "10";
        public bool ForHasStep;
        public ScriptNumericSourceType ForStepType = ScriptNumericSourceType.Number;
        public string ForStepValue = "1";

        public ScriptState Copy() => (ScriptState)MemberwiseClone();
    }

    // Mutable binding state stays private to its action family and is copied as one unit.
    private sealed class ScreenState
    {
        public bool ImageSearchMatchModeWasExplicit;
        public int ScreenX;
        public int ScreenY;
        public int ScreenLeft;
        public int ScreenTop;
        public int ScreenWidth = EditorActionScreenReadingPayload.DefaultSearchScreenWidth;
        public int ScreenHeight = EditorActionScreenReadingPayload.DefaultSearchScreenHeight;
        public string? ImageSearchRegionLeftToken;
        public string? ImageSearchRegionTopToken;
        public string? ImageSearchRegionWidthToken;
        public string? ImageSearchRegionHeightToken;
        public string ScreenColorHex = EditorActionScreenReadingPayload.DefaultColorHex;
        public EditorActionScreenTargetColorSource ScreenTargetColorSource = EditorActionScreenTargetColorSource.ManualHex;
        public string ScreenTargetColorVariableName = EditorActionScreenReadingPayload.DefaultTargetColorVariableName;
        public string ScreenColorVariableName = EditorActionScreenReadingPayload.DefaultColorVariableName;
        public int ScreenTimeoutMs = EditorActionScreenReadingPayload.DefaultTimeoutMs;
        public int ScreenTolerance = EditorActionScreenReadingPayload.DefaultTolerance;
        public string ScreenFoundVariableName = EditorActionScreenReadingPayload.DefaultFoundVariableName;
        public string ScreenFoundXVariableName = EditorActionScreenReadingPayload.DefaultFoundXVariableName;
        public string ScreenFoundYVariableName = EditorActionScreenReadingPayload.DefaultFoundYVariableName;
        public string ImageAssetName = string.Empty;
        public double ImageSearchSimilarity = EditorActionScreenReadingPayload.DefaultImageSearchSimilarity;
        public EditorImageMatchMode ImageSearchMatchMode = EditorImageMatchMode.Automatic;

        public ScreenState Copy() => (ScreenState)MemberwiseClone();
    }

    // Mutable binding state stays private to its action family and is copied as one unit.
    private sealed class ShellState
    {
        public ShellCommandMode ShellCommandMode = ShellCommandMode.Shell;
        public string ShellCommand = string.Empty;
        public string ShellStandardInput = string.Empty;
        public string ShellExitCodeVariableName = "exit_code";
        public string ShellStandardOutputVariableName = "stdout";
        public string ShellStandardErrorVariableName = "stderr";
        public int ShellRetries;
        public int ShellBackoffMs;
        public int ShellTimeoutMs;

        public ShellState Copy() => (ShellState)MemberwiseClone();
    }

    // Mutable binding state stays private to its action family and is copied as one unit.
    private sealed class ScreenshotState
    {
        public string ScreenshotOutputPath = string.Empty;
        public bool ScreenshotCopyToClipboard;
        public bool ScreenshotUseRegion;
        public string ScreenshotRegionX = "0";
        public string ScreenshotRegionY = "0";
        public string ScreenshotRegionWidth = "100";
        public string ScreenshotRegionHeight = "100";

        public ScreenshotState Copy() => (ScreenshotState)MemberwiseClone();
    }

    // Mutable binding state stays private to its action family and is copied as one unit.
    private sealed class WindowState
    {
        public WindowCommandMode WindowCommandMode = WindowCommandMode.Active;
        public string WindowSelectorKind = "title";
        public string WindowSelectorValue = string.Empty;
        public string WindowActiveField = "title";
        public string WindowOutputVariable = "windowResult";
        public int WindowTimeoutMs = 5000;
        public int WindowX;
        public int WindowY;
        public int WindowWidth = 1280;
        public int WindowHeight = 720;
        public string WindowWorkspace = string.Empty;

        public WindowState Copy() => (WindowState)MemberwiseClone();
    }

}
