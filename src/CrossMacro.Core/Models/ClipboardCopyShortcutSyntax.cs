namespace CrossMacro.Core.Models;

/// <summary>
/// Defines the stable run-script tokens for <see cref="ClipboardCopyShortcut"/>.
/// </summary>
public static class ClipboardCopyShortcutSyntax
{
    public const string CtrlCScriptToken = "ctrl+c";
    public const string CtrlShiftCScriptToken = "ctrl+shift+c";

    public static string ToScriptToken(ClipboardCopyShortcut shortcut) => shortcut switch
    {
        ClipboardCopyShortcut.CtrlC => CtrlCScriptToken,
        ClipboardCopyShortcut.CtrlShiftC => CtrlShiftCScriptToken,
        _ => throw new ArgumentOutOfRangeException(nameof(shortcut), shortcut, "Unknown clipboard copy shortcut."),
    };

    public static bool TryParse(string? token, out ClipboardCopyShortcut shortcut)
    {
        var trimmedToken = token?.Trim();
        if (string.Equals(trimmedToken, CtrlCScriptToken, StringComparison.OrdinalIgnoreCase))
        {
            shortcut = ClipboardCopyShortcut.CtrlC;
            return true;
        }

        if (string.Equals(trimmedToken, CtrlShiftCScriptToken, StringComparison.OrdinalIgnoreCase))
        {
            shortcut = ClipboardCopyShortcut.CtrlShiftC;
            return true;
        }

        shortcut = default;
        return false;
    }
}
