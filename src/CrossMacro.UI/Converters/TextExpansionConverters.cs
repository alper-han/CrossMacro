
namespace CrossMacro.UI.Converters;

/// <summary>
/// Converters used by the text expansion tab.
/// </summary>
public static class TextExpansionConverters
{
    /// <summary>
    /// Returns a user-facing label for an insertion mode.
    /// </summary>
    public static readonly IValueConverter InsertionModeDisplayText =
        new LocalizedEnumConverter { KeyPrefix = "TextExpansion_Mode" };

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
        new LocalizedEnumConverter { KeyPrefix = "TextExpansion_DirectTypingMethod" };

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
