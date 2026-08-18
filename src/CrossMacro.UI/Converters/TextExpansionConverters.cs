
namespace CrossMacro.UI.Converters;

/// <summary>
/// Converters used by the text expansion tab.
/// </summary>
public static class TextExpansionConverters
{
    private static string GetLocalizedText(string key, string fallback)
    {
        return Resources.ResourceManager.GetString(key, Resources.Culture) ?? fallback;
    }

    /// <summary>
    /// Returns a user-facing label for an insertion mode.
    /// </summary>
    public static readonly IValueConverter InsertionModeDisplayText =
        new FuncValueConverter<TextInsertionMode, string>(mode => mode switch
        {
            TextInsertionMode.DirectTyping => GetLocalizedText("TextExpansion_ModeDirectTyping", "Direct Typing"),
            TextInsertionMode.Paste => GetLocalizedText("TextExpansion_ModePaste", "Paste"),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, message: null),
        });

    /// <summary>
    /// Returns a user-facing label for a paste method.
    /// </summary>
    public static readonly IValueConverter PasteMethodDisplayText =
        new FuncValueConverter<PasteMethod, string>(method => method switch
        {
            PasteMethod.CtrlShiftV => "Ctrl+Shift+V",
            PasteMethod.ShiftInsert => "Shift+Insert",
            PasteMethod.CtrlV => "Ctrl+V",
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, message: null),
        });

    /// <summary>
    /// Returns a user-facing label for a direct typing method.
    /// </summary>
    public static readonly IValueConverter DirectTypingMethodDisplayText =
        new FuncValueConverter<DirectTypingMethod, string>(method => method switch
        {
            DirectTypingMethod.CompatibleKeyByKey => GetLocalizedText(
                "TextExpansion_DirectTypingMethodCompatible",
                "Compatible (key-by-key)"),
            DirectTypingMethod.FastBatch => GetLocalizedText("TextExpansion_DirectTypingMethodFast", "Fast (batched)"),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, message: null),
        });

    /// <summary>
    /// Returns true when the insertion mode uses clipboard paste.
    /// </summary>
    public static readonly IValueConverter IsPasteMode =
        new FuncValueConverter<TextInsertionMode, bool>(mode => mode is TextInsertionMode.Paste);

    /// <summary>
    /// Returns true when the insertion mode uses direct typing.
    /// </summary>
    public static readonly IValueConverter IsDirectTypingMode =
        new FuncValueConverter<TextInsertionMode, bool>(mode => mode is TextInsertionMode.DirectTyping);
}
