namespace CrossMacro.Application.Automation;

/// <summary>Strictly validates and canonicalizes one shortcut-task chord.</summary>
public interface IShortcutHotkeyNormalizer
{
    /// <summary>Returns false with an actionable validation message when the chord is invalid.</summary>
    public bool TryNormalize(string? hotkey, out string? normalized, out string? validationMessage);
}
