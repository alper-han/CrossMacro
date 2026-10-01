namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignShortcutHotkeyNormalizer : IShortcutHotkeyNormalizer
{
    public bool TryNormalize(string? hotkey, out string? normalized, out string? validationMessage)
    {
        normalized = hotkey?.Trim();
        validationMessage = string.IsNullOrWhiteSpace(normalized) ? "A shortcut chord is required." : null;
        return validationMessage is null;
    }
}
