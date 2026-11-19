namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Creates the editor action a user adds from the picker, with sensible defaults for its type.</summary>
internal static class EditorNewActionFactory
{
    private const int DefaultNewDelayMs = 100;
    private const int DefaultNewRandomDelayMinMs = 50;
    private const int DefaultNewRandomDelayMaxMs = 150;
    private const int DefaultNewWindowTimeoutMs = 5000;
    private const int DefaultNewWindowWidth = 1280;
    private const int DefaultNewWindowHeight = 720;
    private const string DefaultNewClipboardText = "clipboard text";
    private const string DefaultNewShellCommand = "echo hello";

    internal static EditorAction Create(EditorActionType type, bool isAbsolute, IReadOnlyList<string> imageAssetNames)
    {
        var usesImageAsset = type is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage;
        var action = new EditorAction
        {
            Type = type,
            IsAbsolute = isAbsolute,
            DelayMs = type is EditorActionType.Delay ? DefaultNewDelayMs : 0,
            UseRandomDelay = false,
            RandomDelayMinMs = DefaultNewRandomDelayMinMs,
            RandomDelayMaxMs = DefaultNewRandomDelayMaxMs,
            ScrollAmount = type is EditorActionType.ScrollVertical or EditorActionType.ScrollHorizontal ? 1 : 0,
            Text = type is EditorActionType.ClipboardSet ? DefaultNewClipboardText : string.Empty,
            ScriptVariableName = type is EditorActionType.ClipboardGet or EditorActionType.CopySelectionToVariable ? "clipboardText" : "i",
            ClipboardCopyShortcut = ClipboardCopyShortcut.CtrlC,
            MousePositionXVariableName = "mouse_x",
            MousePositionYVariableName = "mouse_y",
            ShellCommand = type is EditorActionType.ShellCommand ? DefaultNewShellCommand : string.Empty,
            ScreenshotCopyToClipboard = type is EditorActionType.Screenshot,
            ShellStandardInput = string.Empty,
            ShellExitCodeVariableName = "exit_code",
            ShellStandardOutputVariableName = "stdout",
            ShellStandardErrorVariableName = "stderr",
            WindowCommandMode = WindowCommandMode.Active,
            WindowActiveField = "title",
            WindowOutputVariable = "windowResult",
            WindowTimeoutMs = DefaultNewWindowTimeoutMs,
            WindowWidth = DefaultNewWindowWidth,
            WindowHeight = DefaultNewWindowHeight,
            ScreenWidth = EditorActionScreenReadingPayload.DefaultPointScreenWidth,
            ScreenHeight = EditorActionScreenReadingPayload.DefaultPointScreenHeight,
            ScreenColorHex = EditorActionScreenReadingPayload.DefaultColorHex,
            ScreenTargetColorSource = EditorActionScreenTargetColorSource.ManualHex,
            ScreenTargetColorVariableName = EditorActionScreenReadingPayload.DefaultTargetColorVariableName,
            ScreenColorVariableName = EditorActionScreenReadingPayload.DefaultColorVariableName,
            ScreenFoundVariableName = EditorActionScreenReadingPayload.DefaultFoundVariableName,
            ScreenFoundXVariableName = EditorActionScreenReadingPayload.DefaultFoundXVariableName,
            ScreenFoundYVariableName = EditorActionScreenReadingPayload.DefaultFoundYVariableName,
            ScreenTimeoutMs = EditorActionScreenReadingPayload.DefaultTimeoutMs,
            ScreenTolerance = EditorActionScreenReadingPayload.DefaultTolerance,
            ImageAssetName = usesImageAsset && imageAssetNames.Count > 0 ? imageAssetNames[0] : string.Empty,
            ImageSearchSimilarity = EditorActionScreenReadingPayload.DefaultImageSearchSimilarity,
            Button = MacroMouseButton.Left,
        };

        if (EditorActionScreenReadingPayload.TryCreateDefault(type, out var screenReadingPayload))
        {
            action.ApplyScreenReadingPayload(screenReadingPayload);

            if (usesImageAsset && imageAssetNames.Count > 0)
            {
                action.ImageAssetName = imageAssetNames[0];
            }
        }

        return action;
    }
}
