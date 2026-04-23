using Avalonia;
using Avalonia.Data.Converters;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.UI.Views.Tabs;

/// <summary>
/// Converters used by the text expansion tab.
/// </summary>
public static class TextExpansionConverters
{
    private static ILocalizationService? GetLocalizationService()
    {
        return (Application.Current as CrossMacro.UI.App)?.Services?.GetService<ILocalizationService>();
    }

    private static string GetLocalizedText(string key, string fallback)
    {
        return GetLocalizationService()?[key] ?? fallback;
    }

    /// <summary>
    /// Returns a user-facing label for an insertion mode.
    /// </summary>
    public static readonly IValueConverter InsertionModeDisplayText =
        new FuncValueConverter<TextInsertionMode, string>(mode => mode switch
        {
            TextInsertionMode.DirectTyping => GetLocalizedText("TextExpansion_ModeDirectTyping", "Direct Typing"),
            _ => GetLocalizedText("TextExpansion_ModePaste", "Paste")
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
