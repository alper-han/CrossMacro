namespace CrossMacro.Core.Models;

/// <summary>Focused-window fields supported by shortcut scope rules.</summary>
public static class ShortcutWindowRuleFields
{
    public static readonly IReadOnlyList<TriggerField> Available =
    [
        TriggerField.WindowClass,
        TriggerField.WindowTitle,
        TriggerField.ProcessName,
    ];
}
