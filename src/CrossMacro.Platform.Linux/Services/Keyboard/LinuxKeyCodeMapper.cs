namespace CrossMacro.Platform.Linux.Services.Keyboard;

/// <summary>
/// Maps Linux evdev keycodes to key names with XKB fallback support.
/// Provides static mappings for modifiers, function keys, numpad, and navigation keys.
/// </summary>
public class LinuxKeyCodeMapper(IXkbStateManager? xkbState = null) : ILinuxKeyCodeMapper
{
    private readonly IXkbStateManager? _xkbState = xkbState;

    public string GetKeyName(int keyCode)
    {
        return GetModifierName(keyCode)
            ?? GetSpecialName(keyCode)
            ?? GetFunctionName(keyCode)
            ?? GetNumpadName(keyCode)
            ?? GetXkbName(keyCode)
            ?? GetFallbackName(keyCode);
    }

    private static string? GetModifierName(int keyCode) => keyCode switch
    {
        29 or 97 => "Ctrl",
        42 or 54 => "Shift",
        56 => "Alt",
        100 => "AltGr",
        125 or 126 => "Super",
        _ => null,
    };

    private static string? GetSpecialName(int keyCode) => keyCode switch
    {
        57 => "Space", 28 => "Enter", 15 => "Tab", 14 => "Backspace", 1 => "Escape",
        111 => "Delete", 110 => "Insert", 102 => "Home", 107 => "End",
        104 => "PageUp", 109 => "PageDown", 103 => "Up", 108 => "Down",
        105 => "Left", 106 => "Right", 58 => "CapsLock", 69 => "NumLock",
        70 => "ScrollLock", 99 => "PrintScreen", 119 => "Pause", 127 => "Menu",
        _ => null,
    };

    private static string? GetFunctionName(int keyCode) => keyCode switch
    {
        >= 59 and <= 68 => $"F{(keyCode - 58).ToString(CultureInfo.InvariantCulture)}",
        87 => "F11",
        88 => "F12",
        >= 183 and <= 194 => $"F{(keyCode - 170).ToString(CultureInfo.InvariantCulture)}",
        _ => null,
    };

    private static string? GetNumpadName(int keyCode) => keyCode switch
    {
        71 => "Numpad7", 72 => "Numpad8", 73 => "Numpad9", 74 => "Numpad-",
        75 => "Numpad4", 76 => "Numpad5", 77 => "Numpad6", 78 => "Numpad+",
        79 => "Numpad1", 80 => "Numpad2", 81 => "Numpad3", 82 => "Numpad0",
        83 => "Numpad.", 96 => "NumpadEnter", 98 => "Numpad/", 55 => "Numpad*", 117 => "Numpad=",
        _ => null,
    };

    private string? GetXkbName(int keyCode)
    {
        if (_xkbState?.IsInitialized is not true)
        {
            return null;
        }

        var utf8 = _xkbState.GetUtf8String((uint)(keyCode + 8));
        if (string.IsNullOrEmpty(utf8))
        {
            return null;
        }

        return utf8.Length is 1 ? utf8.ToUpper(CultureInfo.InvariantCulture) : utf8;
    }

    private static string GetFallbackName(int keyCode)
    {
        if (keyCode is 11)
        {
            return "0";
        }

        if (keyCode is >= 2 and <= 10)
        {
            return (keyCode - 1).ToString(CultureInfo.InvariantCulture);
        }

        if (keyCode is >= 16 and <= 25)
        {
            return "QWERTYUIOP"[keyCode - 16].ToString();
        }

        if (keyCode is >= 30 and <= 38)
        {
            return "ASDFGHJKL"[keyCode - 30].ToString();
        }

        if (keyCode is >= 44 and <= 50)
        {
            return "ZXCVBNM"[keyCode - 44].ToString();
        }

        return $"Key{keyCode.ToString(CultureInfo.InvariantCulture)}";
    }

    public int GetKeyCode(string keyName)
    {
        ArgumentNullException.ThrowIfNull(keyName);
        var special = GetSpecialKeyCode(keyName);
        if (special != -1)
        {
            return special;
        }

        if (TryGetFunctionKeyCode(keyName, out var functionCode))
        {
            return functionCode;
        }

        return FindKeyCodeByName(keyName);
    }

    private static int GetSpecialKeyCode(string keyName) => keyName switch
    {
        "Space" => 57,
        "Enter" or "Return" => 28,
        "Backspace" => 14,
        "Tab" => 15,
        "Escape" or "Esc" => 1,
        "Ctrl" or "LCtrl" => 29,
        "RCtrl" => 97,
        "Shift" or "LShift" => 42,
        "RShift" => 54,
        "Alt" or "LAlt" => 56,
        "AltGr" or "RAlt" => 100,
        "Super" or "LSuper" or "Meta" => 125,
        "RSuper" => 126,
        "CapsLock" => 58,
        "NumLock" => 69,
        "ScrollLock" => 70,
        "PrintScreen" or "PrtSc" => 99,
        "Pause" => 119,
        "Menu" => 127,
        "Delete" or "Del" => 111,
        "Insert" or "Ins" => 110,
        "Home" => 102,
        "End" => 107,
        "PageUp" or "PgUp" => 104,
        "PageDown" or "PgDn" => 109,
        "Up" => 103,
        "Down" => 108,
        "Left" => 105,
        "Right" => 106,
        "Numpad7" => 71,
        "Numpad8" => 72,
        "Numpad9" => 73,
        "Numpad-" => 74,
        "Numpad4" => 75,
        "Numpad5" => 76,
        "Numpad6" => 77,
        "Numpad+" => 78,
        "Numpad1" => 79,
        "Numpad2" => 80,
        "Numpad3" => 81,
        "Numpad0" => 82,
        "Numpad." => 83,
        "NumpadEnter" => 96,
        "Numpad/" => 98,
        "Numpad*" => 55,
        _ => -1,
    };

    private static bool TryGetFunctionKeyCode(string keyName, out int code)
    {
        code = -1;
        if (keyName.Length is 0 || keyName[0] is not ('F' or 'f') ||
            !int.TryParse(keyName[1..], CultureInfo.InvariantCulture, out var functionNumber))
        {
            return false;
        }

        code = functionNumber switch
        {
            >= 1 and <= 10 => 59 + functionNumber - 1,
            11 => 87,
            12 => 88,
            >= 13 and <= 24 => 183 + functionNumber - 13,
            _ => -1,
        };
        return code is not -1;
    }

    private int FindKeyCodeByName(string keyName)
    {
        for (int i = 0; i < 256; i++)
        {
            if (string.Equals(GetKeyName(i), keyName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    public bool IsModifier(int keyCode) => keyCode is 29 or 97 or 42 or 54 or 56 or 100 or 125 or 126;
}
