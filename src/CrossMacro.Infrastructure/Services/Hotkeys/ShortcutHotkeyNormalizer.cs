namespace CrossMacro.Infrastructure.Services.Hotkeys;

/// <summary>Strict normalizer for shortcut-task chords.</summary>
public sealed class ShortcutHotkeyNormalizer(
    IKeyCodeMapper keyCodeMapper,
    IHotkeyStringBuilder hotkeyStringBuilder,
    IMouseButtonMapper mouseButtonMapper) : IShortcutHotkeyNormalizer
{
    private readonly IKeyCodeMapper _keyCodeMapper = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));
    private readonly IHotkeyStringBuilder _hotkeyStringBuilder = hotkeyStringBuilder ?? throw new ArgumentNullException(nameof(hotkeyStringBuilder));
    private readonly IMouseButtonMapper _mouseButtonMapper = mouseButtonMapper ?? throw new ArgumentNullException(nameof(mouseButtonMapper));

    public bool TryNormalize(string? hotkey, out string? normalized, out string? validationMessage)
    {
        normalized = null;
        validationMessage = null;

        if (string.IsNullOrWhiteSpace(hotkey))
        {
            validationMessage = "A shortcut chord is required.";
            return false;
        }

        var modifiers = new HashSet<int>();
        var modifierFamilies = new HashSet<string>(StringComparer.Ordinal);
        var mainKeyCode = -1;
        var mainKeyIsMouse = false;
        foreach (var rawToken in hotkey.Split('+', StringSplitOptions.None))
        {
            var token = rawToken.Trim();
            if (token.Length is 0)
            {
                validationMessage = "Shortcut chords cannot contain empty key tokens.";
                return false;
            }

            var mouseButtonCode = _mouseButtonMapper.GetButtonCode(token);
            var keyCode = mouseButtonCode is -1 ? _keyCodeMapper.GetKeyCode(token) : mouseButtonCode;
            if (keyCode is -1)
            {
                validationMessage = $"Unknown shortcut key: {token}.";
                return false;
            }

            if (_keyCodeMapper.IsModifierKeyCode(keyCode))
            {
                if (!IsSupportedModifier(keyCode))
                {
                    validationMessage = $"Unsupported shortcut modifier: {token}.";
                    return false;
                }

                if (!modifierFamilies.Add(GetModifierFamily(keyCode)))
                {
                    validationMessage = $"Shortcut chords cannot contain duplicate or conflicting modifiers: {token}.";
                    return false;
                }

                _ = modifiers.Add(keyCode);
                continue;
            }

            if (mainKeyCode is not -1)
            {
                validationMessage = "A shortcut chord must contain exactly one main key.";
                return false;
            }

            mainKeyCode = keyCode;
            mainKeyIsMouse = mouseButtonCode is not -1;
        }

        if (mainKeyCode is -1)
        {
            validationMessage = "A shortcut chord must contain one main key.";
            return false;
        }

        if (modifiers.Count is 0 && mainKeyCode is InputEventCode.BTN_LEFT or InputEventCode.BTN_RIGHT)
        {
            validationMessage = "Mouse Left and Mouse Right shortcuts require a modifier.";
            return false;
        }

        normalized = mainKeyIsMouse
            ? _hotkeyStringBuilder.BuildForMouse(_mouseButtonMapper.GetMouseButtonName(mainKeyCode), modifiers)
            : _hotkeyStringBuilder.Build(mainKeyCode, modifiers);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            validationMessage = "The shortcut chord could not be canonicalized.";
            normalized = null;
            return false;
        }

        return true;
    }

    private static string GetModifierFamily(int keyCode) => keyCode switch
    {
        InputEventCode.KEY_LEFTCTRL or InputEventCode.KEY_RIGHTCTRL => "Ctrl",
        InputEventCode.KEY_LEFTSHIFT or InputEventCode.KEY_RIGHTSHIFT => "Shift",
        InputEventCode.KEY_LEFTALT => "Alt",
        InputEventCode.KEY_RIGHTALT => "AltGr",
        InputEventCode.KEY_LEFTMETA or InputEventCode.KEY_RIGHTMETA => "Super",
        _ => throw new ArgumentOutOfRangeException(nameof(keyCode), keyCode, "Unsupported modifier."),
    };

    private static bool IsSupportedModifier(int keyCode) => keyCode is
        InputEventCode.KEY_LEFTCTRL or InputEventCode.KEY_RIGHTCTRL or
        InputEventCode.KEY_LEFTSHIFT or InputEventCode.KEY_RIGHTSHIFT or
        InputEventCode.KEY_LEFTALT or InputEventCode.KEY_RIGHTALT or
        InputEventCode.KEY_LEFTMETA or InputEventCode.KEY_RIGHTMETA;
}
