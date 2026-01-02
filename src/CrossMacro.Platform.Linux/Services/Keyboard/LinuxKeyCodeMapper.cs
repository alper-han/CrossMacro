namespace CrossMacro.Platform.Linux.Services.Keyboard;

/// <summary>
/// Maps Linux evdev keycodes to key names with XKB fallback support.
/// Provides static mappings for modifiers, function keys, numpad, and navigation keys.
/// </summary>
public class LinuxKeyCodeMapper : ILinuxKeyCodeMapper
{
    private readonly IXkbStateManager? _xkbState;

    public LinuxKeyCodeMapper(IXkbStateManager? xkbState = null)
    {
        _xkbState = xkbState;
    }

    public string GetKeyName(int keyCode)
    {
        // 1. Modifier keys - always return consistent names
        var modifierName = keyCode switch
        {
            29 => "Ctrl", 97 => "Ctrl", 42 => "Shift", 54 => "Shift",
            56 => "Alt", 100 => "AltGr", 125 => "Super", 126 => "Super",
            _ => null
        };
        if (modifierName != null) return modifierName;

        // 2. Special/Navigation keys
        var special = keyCode switch
        {
            57 => "Space", 28 => "Enter", 15 => "Tab", 14 => "Backspace", 1 => "Escape",
            111 => "Delete", 110 => "Insert", 102 => "Home", 107 => "End",
            104 => "PageUp", 109 => "PageDown",
            103 => "Up", 108 => "Down", 105 => "Left", 106 => "Right",
            58 => "CapsLock", 69 => "NumLock", 70 => "ScrollLock",
            99 => "PrintScreen", 119 => "Pause", 127 => "Menu",
            _ => null
        };
        if (special != null) return special;

        // 3. Function keys (F1-F10: 59-68, F11: 87, F12: 88, F13-F24: 183-194)
        if (keyCode >= 59 && keyCode <= 68) return "F" + (keyCode - 58);
        if (keyCode == 87) return "F11";
        if (keyCode == 88) return "F12";
        if (keyCode >= 183 && keyCode <= 194) return "F" + (keyCode - 170);

        // 4. Numpad
        var numpad = keyCode switch
        {
            71 => "Numpad7", 72 => "Numpad8", 73 => "Numpad9", 74 => "Numpad-",
            75 => "Numpad4", 76 => "Numpad5", 77 => "Numpad6", 78 => "Numpad+",
            79 => "Numpad1", 80 => "Numpad2", 81 => "Numpad3",
            82 => "Numpad0", 83 => "Numpad.", 96 => "NumpadEnter",
            98 => "Numpad/", 55 => "Numpad*", 117 => "Numpad=",
            _ => null
        };
        if (numpad != null) return numpad;

        // 5. Try XKB for character keys
        if (_xkbState?.IsInitialized == true)
        {
            var utf8 = _xkbState.GetUtf8String((uint)(keyCode + 8));
            if (!string.IsNullOrEmpty(utf8)) return utf8.Length == 1 ? utf8.ToUpper() : utf8;
        }

        // 6. Digits fallback
        if (keyCode == 11) return "0";
        if (keyCode >= 2 && keyCode <= 10) return (keyCode - 1).ToString();

        // 7. Letters fallback (QWERTY)
        if (keyCode >= 16 && keyCode <= 25) return "QWERTYUIOP"[keyCode - 16].ToString();
        if (keyCode >= 30 && keyCode <= 38) return "ASDFGHJKL"[keyCode - 30].ToString();
        if (keyCode >= 44 && keyCode <= 50) return "ZXCVBNM"[keyCode - 44].ToString();

        return $"Key{keyCode}";
    }

    public int GetKeyCode(string keyName)
    {
        // Special keys
        var special = keyName switch
        {
            "Space" => 57, "Enter" or "Return" => 28, "Backspace" => 14, "Tab" => 15, "Escape" or "Esc" => 1,
            "Ctrl" or "LCtrl" => 29, "RCtrl" => 97, "Shift" or "LShift" => 42, "RShift" => 54,
            "Alt" or "LAlt" => 56, "AltGr" or "RAlt" => 100, "Super" or "LSuper" or "Meta" => 125, "RSuper" => 126,
            "CapsLock" => 58, "NumLock" => 69, "ScrollLock" => 70,
            "PrintScreen" or "PrtSc" => 99, "Pause" => 119, "Menu" => 127,
            "Delete" or "Del" => 111, "Insert" or "Ins" => 110,
            "Home" => 102, "End" => 107, "PageUp" or "PgUp" => 104, "PageDown" or "PgDn" => 109,
            "Up" => 103, "Down" => 108, "Left" => 105, "Right" => 106,
            // Numpad
            "Numpad7" => 71, "Numpad8" => 72, "Numpad9" => 73, "Numpad-" => 74,
            "Numpad4" => 75, "Numpad5" => 76, "Numpad6" => 77, "Numpad+" => 78,
            "Numpad1" => 79, "Numpad2" => 80, "Numpad3" => 81,
            "Numpad0" => 82, "Numpad." => 83, "NumpadEnter" => 96, "Numpad/" => 98, "Numpad*" => 55,
            _ => -1
        };
        if (special != -1) return special;

        // Function keys
        if (keyName.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(keyName[1..], out var fNum))
        {
            if (fNum >= 1 && fNum <= 10) return 59 + fNum - 1;
            if (fNum == 11) return 87;
            if (fNum == 12) return 88;
            if (fNum >= 13 && fNum <= 24) return 183 + fNum - 13;
        }

        // Reverse lookup via GetKeyName
        for (int i = 0; i < 256; i++)
        {
            if (string.Equals(GetKeyName(i), keyName, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    public bool IsModifier(int keyCode) => keyCode is 29 or 97 or 42 or 54 or 56 or 100 or 125 or 126;
}
