using CrossMacro.Application.Automation;

namespace CrossMacro.TestInfrastructure;

public sealed class AcceptingShortcutHotkeyNormalizer : IShortcutHotkeyNormalizer
{
    public bool TryNormalize(string? hotkey, out string? normalized, out string? validationMessage)
    {
        normalized = hotkey?.Trim();
        validationMessage = string.IsNullOrWhiteSpace(normalized) ? "A shortcut chord is required." : null;
        return validationMessage is null;
    }
}
