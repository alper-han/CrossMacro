namespace CrossMacro.Platform.MacOS.Services;

/// <summary>
/// Pure key-name and modifier policies shared by the macOS keyboard layout adapter.
/// Native layout loading, translation and retained CoreFoundation ownership remain
/// in <see cref="MacKeyboardLayoutService"/>.
/// </summary>
internal static class MacKeyboardLayoutPolicy
{
    internal static bool TryGetModifierKeyCode(string keyName, out int code)
    {
        if (keyName.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
        {
            code = 29;
            return true;
        }

        if (keyName.Equals("Shift", StringComparison.OrdinalIgnoreCase))
        {
            code = 42;
            return true;
        }

        if (keyName.Equals("Alt", StringComparison.OrdinalIgnoreCase) ||
            keyName.Equals("Option", StringComparison.OrdinalIgnoreCase))
        {
            code = 56;
            return true;
        }

        if (keyName.Equals("Command", StringComparison.OrdinalIgnoreCase) ||
            keyName.Equals("Super", StringComparison.OrdinalIgnoreCase))
        {
            code = 125;
            return true;
        }

        code = -1;
        return false;
    }

    internal static bool TryGetFunctionKeyCode(string keyName, out int code)
    {
        code = -1;
        if (keyName.Length <= 1 || (keyName[0] is not 'F' and not 'f'))
        {
            return false;
        }

        if (!int.TryParse(keyName[1..], CultureInfo.InvariantCulture, out var fNum))
        {
            return false;
        }

        if (fNum is >= 1 and <= 10)
        {
            code = 59 + fNum - 1;
            return true;
        }

        if (fNum is 11)
        {
            code = 87;
            return true;
        }

        if (fNum is 12)
        {
            code = 88;
            return true;
        }

        if (fNum is >= 13 and <= 20)
        {
            code = 183 + fNum - 13;
            return true;
        }

        return false;
    }

    internal static int GetSpecialKeyCode(string keyName) => keyName switch
    {
        "Space" => 57,
        "Enter" => 28,
        "Tab" => 15,
        "Backspace" => 14,
        "Escape" or "Esc" => 1,
        "Delete" or "Del" => 111,
        "Insert" => 110,
        "Home" => 102,
        "End" => 107,
        "PageUp" => 104,
        "PageDown" => 109,
        "Up" => 103,
        "Down" => 108,
        "Left" => 105,
        "Right" => 106,
        "CapsLock" => 58,
        "NumLock" => 69,
        "Help" => 138,
        "Mute" => 113,
        "VolumeDown" => 114,
        "VolumeUp" => 115,
        "BrightnessDown" => InputEventCode.KEY_BRIGHTNESSDOWN,
        "BrightnessUp" => InputEventCode.KEY_BRIGHTNESSUP,
        "PlayPause" => 164,
        "PreviousSong" => 165,
        "NextSong" => 163,
        "Rewind" => InputEventCode.KEY_REWIND,
        "FastForward" => InputEventCode.KEY_FASTFORWARD,
        "ISOSection" => 86,
        "Yen" => 124,
        "NumpadJpComma" => 95,
        "Numpad7" => 71,
        "Numpad8" => 72,
        "Numpad9" => 73,
        "Numpad-" => 74,
        "Numpad4" => 75,
        "Numpad5" => 76,
        "Numpad6" => 77,
        "Numpad+" or "NumpadPlus" => 78,
        "Numpad1" => 79,
        "Numpad2" => 80,
        "Numpad3" => 81,
        "Numpad0" => 82,
        "Numpad." => 83,
        "NumpadEnter" => 96,
        "Numpad/" => 98,
        "Numpad*" => 55,
        "Numpad=" => 117,
        _ => -1,
    };

    internal static string? GetSemanticKeyName(int keyCode)
        => GetModifierKeyName(keyCode)
        ?? GetNavigationKeyName(keyCode)
        ?? GetMediaAndSpecialKeyName(keyCode)
        ?? GetFunctionKeyName(keyCode)
        ?? GetNumpadKeyName(keyCode);

    internal static uint BuildUCKeyModifierState(bool shift, bool option, bool capsLock, bool leftCtrl)
    {
        uint modifierState = 0;
        if (capsLock)
        {
            modifierState |= 1u << 10;
        }

        if (shift)
        {
            modifierState |= 1u << 9;
        }

        if (option)
        {
            modifierState |= 1u << 11;
        }

        if (leftCtrl)
        {
            modifierState |= 1u << 12;
        }

        return (modifierState >> 8) & 0xFF;
    }

    internal static bool IsModifier(int keyCode)
        => keyCode is 29 or 97 or 42 or 54 or 56 or 100 or 125 or 126;

    private static string? GetModifierKeyName(int keyCode) => keyCode switch
    {
        29 or 97 => "Ctrl",
        42 or 54 => "Shift",
        56 or 100 => "Alt",
        125 or 126 => "Command",
        _ => null,
    };

    private static string? GetNavigationKeyName(int keyCode) => keyCode switch
    {
        57 => "Space",
        28 => "Enter",
        15 => "Tab",
        14 => "Backspace",
        1 => "Escape",
        111 => "Delete",
        110 => "Insert",
        102 => "Home",
        107 => "End",
        104 => "PageUp",
        109 => "PageDown",
        103 => "Up",
        108 => "Down",
        105 => "Left",
        106 => "Right",
        58 => "CapsLock",
        69 => "NumLock",
        70 => "ScrollLock",
        99 => "PrintScreen",
        119 => "Pause",
        138 => "Help",
        _ => null,
    };

    private static string? GetMediaAndSpecialKeyName(int keyCode) => keyCode switch
    {
        113 => "Mute",
        114 => "VolumeDown",
        115 => "VolumeUp",
        InputEventCode.KEY_BRIGHTNESSDOWN => "BrightnessDown",
        InputEventCode.KEY_BRIGHTNESSUP => "BrightnessUp",
        164 => "PlayPause",
        165 => "PreviousSong",
        163 => "NextSong",
        InputEventCode.KEY_REWIND => "Rewind",
        InputEventCode.KEY_FASTFORWARD => "FastForward",
        86 => "ISOSection",
        124 => "Yen",
        95 => "NumpadJpComma",
        _ => null,
    };

    private static string? GetFunctionKeyName(int keyCode) => keyCode switch
    {
        59 => "F1",
        60 => "F2",
        61 => "F3",
        62 => "F4",
        63 => "F5",
        64 => "F6",
        65 => "F7",
        66 => "F8",
        67 => "F9",
        68 => "F10",
        87 => "F11",
        88 => "F12",
        183 => "F13",
        184 => "F14",
        185 => "F15",
        186 => "F16",
        187 => "F17",
        188 => "F18",
        189 => "F19",
        190 => "F20",
        _ => null,
    };

    private static string? GetNumpadKeyName(int keyCode) => keyCode switch
    {
        71 => "Numpad7",
        72 => "Numpad8",
        73 => "Numpad9",
        74 => "Numpad-",
        75 => "Numpad4",
        76 => "Numpad5",
        77 => "Numpad6",
        78 => "NumpadPlus",
        79 => "Numpad1",
        80 => "Numpad2",
        81 => "Numpad3",
        82 => "Numpad0",
        83 => "Numpad.",
        96 => "NumpadEnter",
        98 => "Numpad/",
        55 => "Numpad*",
        117 => "Numpad=",
        _ => null,
    };
}
