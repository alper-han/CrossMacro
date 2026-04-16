using Avalonia.Data.Converters;
using CrossMacro.Core.Models;

namespace CrossMacro.UI.Views.Tabs;

/// <summary>
/// Converters used by the text expansion tab.
/// </summary>
public static class TextExpansionConverters
{
    /// <summary>
    /// Returns a user-facing label for an insertion mode.
    /// </summary>
    public static readonly IValueConverter InsertionModeDisplayText =
        new FuncValueConverter<TextInsertionMode, string>(mode => mode switch
        {
            TextInsertionMode.DirectTyping => "Direct Typing",
            _ => "Paste"
        });

    /// <summary>
    /// Returns a user-facing label for a paste method.
    /// </summary>
    public static readonly IValueConverter PasteMethodDisplayText =
        new FuncValueConverter<PasteMethod, string>(method => method switch
        {
            PasteMethod.CtrlShiftV => "Ctrl+Shift+V",
            PasteMethod.ShiftInsert => "Shift+Insert",
            _ => "Ctrl+V"
        });

    /// <summary>
    /// Returns true when the insertion mode uses clipboard paste.
    /// </summary>
    public static readonly IValueConverter IsPasteMode =
        new FuncValueConverter<TextInsertionMode, bool>(mode => mode == TextInsertionMode.Paste);
}
