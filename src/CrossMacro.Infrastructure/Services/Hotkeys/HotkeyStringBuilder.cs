
namespace CrossMacro.Infrastructure.Services.Hotkeys;

/// <summary>
/// Builds human-readable hotkey strings from key codes and modifiers.
/// </summary>
public sealed class HotkeyStringBuilder(IKeyCodeMapper keyCodeMapper)
{
    private readonly IKeyCodeMapper _keyCodeMapper = keyCodeMapper;

    public string Build(int keyCode, IReadOnlySet<int> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        var parts = BuildModifierParts(modifiers);
        parts.Add(_keyCodeMapper.GetKeyName(keyCode));
        return string.Join('+', parts);
    }

    public static string BuildForMouse(string buttonName, IReadOnlySet<int> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        var parts = BuildModifierParts(modifiers);
        parts.Add(buttonName);
        return string.Join('+', parts);
    }

    private static List<string> BuildModifierParts(IReadOnlySet<int> modifiers)
    {
        List<string> parts = [];

        if (modifiers.Contains(InputEventCode.KEY_LEFTCTRL) || modifiers.Contains(InputEventCode.KEY_RIGHTCTRL))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.Contains(InputEventCode.KEY_LEFTSHIFT) || modifiers.Contains(InputEventCode.KEY_RIGHTSHIFT))
        {
            parts.Add("Shift");
        }

        if (modifiers.Contains(InputEventCode.KEY_LEFTALT))
        {
            parts.Add("Alt");
        }

        if (modifiers.Contains(InputEventCode.KEY_RIGHTALT))
        {
            parts.Add("AltGr");
        }

        if (modifiers.Contains(InputEventCode.KEY_LEFTMETA) || modifiers.Contains(InputEventCode.KEY_RIGHTMETA))
        {
            parts.Add("Super");
        }

        return parts;
    }
}
