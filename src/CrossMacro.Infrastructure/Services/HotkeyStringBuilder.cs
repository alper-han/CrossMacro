using CrossMacro.Core.Services;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Builds human-readable hotkey strings from key codes and modifiers.
/// </summary>
public class HotkeyStringBuilder : IHotkeyStringBuilder
{
    private readonly IKeyCodeMapper _keyCodeMapper;
    
    // Modifier key codes (Linux evdev)
    private const int LeftCtrl = 29;
    private const int RightCtrl = 97;
    private const int LeftShift = 42;
    private const int RightShift = 54;
    private const int LeftAlt = 56;
    private const int RightAlt = 100; // AltGr
    private const int LeftSuper = 125;
    private const int RightSuper = 126;
    
    public HotkeyStringBuilder(IKeyCodeMapper keyCodeMapper)
    {
        _keyCodeMapper = keyCodeMapper;
    }
    
    public string Build(int keyCode, IReadOnlySet<int> modifiers)
    {
        var parts = BuildModifierParts(modifiers);
        parts.Add(_keyCodeMapper.GetKeyName(keyCode));
        return string.Join("+", parts);
    }
    
    public string BuildForMouse(string buttonName, IReadOnlySet<int> modifiers)
    {
        var parts = BuildModifierParts(modifiers);
        parts.Add(buttonName);
        return string.Join("+", parts);
    }
    
    private static List<string> BuildModifierParts(IReadOnlySet<int> modifiers)
    {
        List<string> parts = [];

        if (modifiers.Contains(LeftCtrl) || modifiers.Contains(RightCtrl)) 
            parts.Add("Ctrl");
        if (modifiers.Contains(LeftShift) || modifiers.Contains(RightShift)) 
            parts.Add("Shift");
        if (modifiers.Contains(LeftAlt)) 
            parts.Add("Alt");
        if (modifiers.Contains(RightAlt)) 
            parts.Add("AltGr");
        if (modifiers.Contains(LeftSuper) || modifiers.Contains(RightSuper)) 
            parts.Add("Super");

        return parts;
    }
}
