namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// Which property editors the selected action needs, plus the labels and hints that go with them.
/// Every member is derived from the selected action, so the owner raises a single "all changed" notification.
/// </summary>
public sealed class EditorFieldVisibility : ObservableObject
{
    private readonly EditorViewModel _owner;
    private readonly EditorRawScriptHints _hints;

    internal EditorFieldVisibility(EditorViewModel owner, EditorRawScriptHints hints)
    {
        _owner = owner;
        _hints = hints;
    }

    private EditorAction? SelectedAction => _owner.SelectedAction;

    private string Localize(string key) => _owner.Localize(key);

    /// <summary>
    /// Show coordinates for: MouseMove, MouseClick, MouseDown, MouseUp.
    /// </summary>
    public bool ShowCoordinates => SelectedAction is not null && EditorActionCoordinateRules.UsesCoordinates(SelectedAction.Type)
        && !EditorActionCoordinateRules.IsCurrentPositionMouseButton(SelectedAction);

    /// <summary>
    /// Show Absolute/Relative toggle for all coordinate-bearing mouse actions.
    /// </summary>
    public bool ShowCoordModeToggle => SelectedAction is not null && EditorActionCoordinateRules.UsesCoordinates(SelectedAction.Type)
        && !EditorActionCoordinateRules.IsCurrentPositionMouseButton(SelectedAction);

    /// <summary>
    /// Show current-position toggle for mouse button actions.
    /// </summary>
    public bool ShowCurrentPositionToggle => SelectedAction?.Type is
        EditorActionType.MouseClick or
        EditorActionType.MouseDown or
        EditorActionType.MouseUp;
    public string CurrentPositionToggleLabel => Localize(EditorActionPresentation.For(SelectedAction?.Type).CurrentPositionLabelKey);

    /// <summary>
    /// Show mouse button for: MouseClick, ImageClick, MouseDown, MouseUp
    /// </summary>
    public bool ShowMouseButton => SelectedAction?.Type is
        EditorActionType.MouseClick or
        EditorActionType.ImageClick or
        EditorActionType.MouseDown or
        EditorActionType.MouseUp;
    public bool ShowImageClickButton => (SelectedAction?.Type) is EditorActionType.ImageClick;

    /// <summary>
    /// Show key code for: KeyPress, KeyDown, KeyUp
    /// </summary>
    public bool ShowKeyCode => SelectedAction?.Type is
        EditorActionType.KeyPress or
        EditorActionType.KeyDown or
        EditorActionType.KeyUp;

    /// <summary>
    /// Show delay for: Delay action only (other actions have timing handled differently)
    /// </summary>
    public bool ShowDelay => (SelectedAction?.Type) is EditorActionType.Delay;

    /// <summary>
    /// Show fixed delay value when random delay is disabled.
    /// </summary>
    public bool ShowFixedDelayInput => ShowDelay && (SelectedAction?.UseRandomDelay) is not true;

    /// <summary>
    /// Show random delay bounds when random delay is enabled.
    /// </summary>
    public bool ShowRandomDelayOptions => ShowDelay && (SelectedAction?.UseRandomDelay) is true;

    /// <summary>
    /// Show scroll amount for: ScrollVertical, ScrollHorizontal
    /// </summary>
    public bool ShowScrollAmount => SelectedAction?.Type is
        EditorActionType.ScrollVertical or
        EditorActionType.ScrollHorizontal;

    /// <summary>
    /// Show text payload field for TextInput, RawScriptStep, and ClipboardSet.
    /// </summary>
    public bool ShowTextInput => SelectedAction?.Type is EditorActionType.TextInput or EditorActionType.RawScriptStep or EditorActionType.ClipboardSet;
    public bool ShowSetVariableFields => (SelectedAction?.Type) is EditorActionType.SetVariable;
    public bool ShowClipboardGetFields => (SelectedAction?.Type) is EditorActionType.ClipboardGet;
    public bool ShowCopySelectionToVariableFields => (SelectedAction?.Type) is EditorActionType.CopySelectionToVariable;
    public bool ShowMousePositionFields => (SelectedAction?.Type) is EditorActionType.MousePosition;
    public bool ShowScreenshotFields => (SelectedAction?.Type) is EditorActionType.Screenshot;
    public bool ShowScreenshotRegionFields => ShowScreenshotFields && (SelectedAction?.ScreenshotUseRegion) is true;
    public bool ShowShellCommandFields => (SelectedAction?.Type) is EditorActionType.ShellCommand;
    public bool ShowShellStandardInputFields => ShowShellCommandFields
        && SelectedAction?.ShellCommandMode is ShellCommandMode.ShellInput or ShellCommandMode.ShellCaptureInput;
    public bool ShowShellCaptureFields => ShowShellCommandFields
        && SelectedAction?.ShellCommandMode is ShellCommandMode.ShellCapture or ShellCommandMode.ShellCaptureInput;
    public bool ShowWindowCommandFields => (SelectedAction?.Type) is EditorActionType.WindowCommand;
    public bool ShowWindowSelectorFields => ShowWindowCommandFields
        && SelectedAction?.WindowCommandMode is WindowCommandMode.Search or WindowCommandMode.Wait or WindowCommandMode.Focus or WindowCommandMode.Close;
    public bool ShowWindowSearchSelectorKinds => ShowWindowCommandFields
        && SelectedAction?.WindowCommandMode is WindowCommandMode.Search or WindowCommandMode.Wait;
    public bool ShowWindowFocusSelectorKinds => ShowWindowCommandFields && (SelectedAction?.WindowCommandMode) is WindowCommandMode.Focus;
    public bool ShowWindowCloseSelectorKinds => ShowWindowCommandFields && (SelectedAction?.WindowCommandMode) is WindowCommandMode.Close;
    public bool ShowWindowSelectorValueField => ShowWindowSelectorFields && WindowSelectorSyntax.RequiresValue(WindowSelectorSyntax.ParseCanonical(SelectedAction?.WindowSelectorKind));
    public bool ShowWindowActiveFieldSelector => ShowWindowCommandFields && (SelectedAction?.WindowCommandMode) is WindowCommandMode.Active;
    public bool ShowWindowCoordinateFields => ShowWindowCommandFields && (SelectedAction?.WindowCommandMode) is WindowCommandMode.Move;
    public bool ShowWindowDimensionFields => ShowWindowCommandFields && (SelectedAction?.WindowCommandMode) is WindowCommandMode.Resize;
    public bool ShowWindowTimeoutField => ShowWindowCommandFields && (SelectedAction?.WindowCommandMode) is WindowCommandMode.Wait;
    public bool ShowWindowOutputVariableField => ShowWindowCommandFields
        && SelectedAction?.WindowCommandMode is WindowCommandMode.Active or WindowCommandMode.Search or WindowCommandMode.Wait or WindowCommandMode.WorkspaceGet;
    public bool ShowWindowWorkspaceField => ShowWindowCommandFields
        && SelectedAction?.WindowCommandMode is WindowCommandMode.WorkspaceSwitch or WindowCommandMode.WorkspaceMoveActive or WindowCommandMode.WorkspaceMoveWindow;
    public bool ShowWindowAddressField => ShowWindowCommandFields && (SelectedAction?.WindowCommandMode) is WindowCommandMode.WorkspaceMoveWindow;
    public bool ShowIncDecFields => SelectedAction?.Type is EditorActionType.IncrementVariable or EditorActionType.DecrementVariable or EditorActionType.MultiplyVariable or EditorActionType.DivideVariable;
    public bool ShowRepeatFields => (SelectedAction?.Type) is EditorActionType.RepeatBlockStart;
    public bool ShowConditionFields => SelectedAction?.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart;
    public bool ShowForFields => (SelectedAction?.Type) is EditorActionType.ForBlockStart;
    public bool ShowForStepFields => ShowForFields && (SelectedAction?.ForHasStep) is true;

    public string TextInputLabel => Localize(EditorActionPresentation.For(SelectedAction?.Type).TextInputLabelKey);

    public string TextInputWatermark => Localize(EditorActionPresentation.For(SelectedAction?.Type).TextInputWatermarkKey);

    public string TextInputHint
    {
        get
        {
            var presentation = EditorActionPresentation.For(SelectedAction?.Type);
            return SelectedAction?.Type is EditorActionType.RawScriptStep && _hints.TryGetRawScriptHint(SelectedAction.Text, out var hint)
                ? hint
                : Localize(presentation.TextInputHintKey);
        }
    }

    public bool TextInputAcceptsReturn => SelectedAction?.Type is EditorActionType.TextInput or EditorActionType.RawScriptStep or EditorActionType.ClipboardSet;

    public IEnumerable<ScriptConditionOperator> ScriptConditionOperators => _owner.ConditionRules.GetOperators(SelectedAction);

    public string ConditionRightOperandHint => SelectedAction?.ScriptLeftOperandType is ScriptOperandType.Color
        || SelectedAction?.ScriptRightOperandType is ScriptOperandType.Color
        ? Localize("Editor_ConditionColorHint")
        : string.Empty;

    public static IReadOnlyList<EditorActionScreenTargetColorSource> ScreenTargetColorSources { get; } =
        [
            EditorActionScreenTargetColorSource.ManualHex,
            EditorActionScreenTargetColorSource.Variable,
        ];
    public IReadOnlyList<EditorImageMatchMode> ImageMatchModes { get; } =
    [
        EditorImageMatchMode.Automatic,
        EditorImageMatchMode.FirstThresholdMatch,
        EditorImageMatchMode.BestMatch,
    ];
    public bool ShowPixelColorFields => (SelectedAction?.Type) is EditorActionType.PixelColor;
    public bool ShowWaitColorFields => (SelectedAction?.Type) is EditorActionType.WaitColor;
    public bool ShowPixelSearchFields => (SelectedAction?.Type) is EditorActionType.PixelSearch;
    public bool ShowImageSearchFields => SelectedAction?.Type is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage;
    public bool ShowImageOutputVariableFields => SelectedAction?.Type is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage;
    public bool ShowImageWaitTimeoutField => SelectedAction?.Type is EditorActionType.ImageClick or EditorActionType.WaitImage;
    public bool ShowScreenReadingFields => ShowPixelColorFields || ShowWaitColorFields || ShowPixelSearchFields || ShowImageSearchFields;
    public bool ShowScreenReadingColorFields => ShowWaitColorFields || ShowPixelSearchFields;
    public bool ShowScreenReadingPointFields => ShowPixelColorFields || ShowWaitColorFields;
    public bool ShowScreenTargetColorHexInput => ShowScreenReadingColorFields
        && (SelectedAction?.ScreenTargetColorSource) is EditorActionScreenTargetColorSource.ManualHex;
    public bool ShowScreenTargetColorVariableInput => ShowScreenReadingColorFields
        && (SelectedAction?.ScreenTargetColorSource) is EditorActionScreenTargetColorSource.Variable;
    public bool ShowScreenTargetColorVariablePicker => ShowScreenTargetColorVariableInput && _owner.Variables.HasAvailableColorNames;
    public bool ShowScreenReadingRawAssistance => (SelectedAction?.Type) is EditorActionType.RawScriptStep
        && _hints.TryGetRawScreenReadingHint(SelectedAction.Text, out _);
    public string ScreenReadingRawHint => (SelectedAction?.Type) is EditorActionType.RawScriptStep
        && _hints.TryGetRawScreenReadingHint(SelectedAction.Text, out var hint)
            ? hint
            : string.Empty;
    public bool ShowScreenReadingColorPreview => !string.IsNullOrWhiteSpace(ScreenReadingColorPreviewHex);
    public string ScreenReadingColorPreviewHex => GetScreenReadingColorPreviewHex();

    internal void NotifyChanged() => OnPropertyChanged(string.Empty);

    private string GetScreenReadingColorPreviewHex()
    {
        if (SelectedAction is null)
        {
            return string.Empty;
        }

        if (SelectedAction.TryGetScreenReadingPayload(out var payload)
            && payload.UsesTargetColor
            && payload.ScreenTargetColorSource is EditorActionScreenTargetColorSource.ManualHex)
        {
            return EditorRawScriptHints.NormalizePreviewColor(payload.ScreenColorHex);
        }

        return SelectedAction.Type is EditorActionType.RawScriptStep
            && EditorRawScriptHints.TryExtractRawScreenReadingColor(SelectedAction.Text, out var colorHex)
                ? colorHex
                : string.Empty;
    }
}
