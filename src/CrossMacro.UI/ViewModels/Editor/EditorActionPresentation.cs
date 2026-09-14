namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Resource keys for action presentation; localization remains live in the ViewModel.</summary>
internal sealed record EditorActionPresentation(
    string CurrentPositionLabelKey,
    string TextInputLabelKey,
    string TextInputWatermarkKey,
    string TextInputHintKey)
{
    private static readonly EditorActionPresentation Default = new(
        "Editor_CurrentPositionUse", "Editor_TextToType", "Editor_EnterTextToType", "Editor_TextToTypeHint");

    private static readonly IReadOnlyDictionary<EditorActionType, EditorActionPresentation> Overrides =
        new Dictionary<EditorActionType, EditorActionPresentation>
        {
            [EditorActionType.MouseClick] = Default with { CurrentPositionLabelKey = "Editor_CurrentPositionClick" },
            [EditorActionType.MouseDown] = Default with { CurrentPositionLabelKey = "Editor_CurrentPositionHold" },
            [EditorActionType.MouseUp] = Default with { CurrentPositionLabelKey = "Editor_CurrentPositionRelease" },
            [EditorActionType.RawScriptStep] = Default with
            {
                TextInputLabelKey = "Editor_RawScriptStep",
                TextInputWatermarkKey = "Editor_OriginalScriptLine",
                TextInputHintKey = "Editor_RawScriptHint",
            },
            [EditorActionType.ClipboardSet] = Default with
            {
                TextInputLabelKey = "Editor_ClipboardText",
                TextInputWatermarkKey = "Editor_ClipboardTextPlaceholder",
                TextInputHintKey = "Editor_ClipboardSetHint",
            },
        };

    internal static EditorActionPresentation For(EditorActionType? type)
    {
        if (type is not { } actionType) { return Default; }
        if (!Enum.IsDefined(actionType)) { throw new InvalidOperationException("Unsupported editor action type."); }
        return Overrides.TryGetValue(actionType, out var presentation) ? presentation : Default;
    }
}
