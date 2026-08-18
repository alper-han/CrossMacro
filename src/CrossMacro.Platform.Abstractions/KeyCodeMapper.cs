
namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Maps between key names and key codes.
/// Supports keyboard layout-aware key name resolution.
/// </summary>
public class KeyCodeMapper(IKeyboardLayoutService layoutService) : IKeyCodeMapper
{
    private readonly IKeyboardLayoutService _layoutService = layoutService;

    // Modifier key codes (Linux evdev)
    private static readonly HashSet<int> ModifierKeyCodes = new()
    {
        29,  // Left Ctrl
        97,  // Right Ctrl
        42,  // Left Shift
        54,  // Right Shift
        56,  // Left Alt
        100, // Right Alt (AltGr)
        125, // Left Super
        126,  // Right Super
    };

    public int GetKeyCode(string keyName)
    {
        ArgumentNullException.ThrowIfNull(keyName);
        if (TryGetModifierKeyCode(keyName, out var modifierCode))
        {
            return modifierCode;
        }

        if (TryGetFunctionKeyCode(keyName, out var functionCode))
        {
            return functionCode;
        }

        // Special keys
        var special = GetSpecialKeyCode(keyName);
        if (special != -1)
        {
            return special;
        }

        // Mouse buttons
        var mouseCode = GetMouseButtonCode(keyName);
        if (mouseCode != -1)
        {
            return mouseCode;
        }

        // Try layout service first
        var code = _layoutService.GetKeyCode(keyName);
        if (code != -1)
        {
            return code;
        }

        // Letter keys (QWERTY layout fallback)
        if (keyName.Length is 1 && char.IsLetter(keyName[0]))
        {
            return GetLetterKeyCode(char.ToUpper(keyName[0], CultureInfo.InvariantCulture));
        }

        // Digit keys
        if (keyName.Length is 1 && char.IsDigit(keyName[0]))
        {
            var digit = keyName[0] - '0';
            return digit is 0 ? 11 : 2 + digit - 1;
        }

        // Punctuation
        return GetPunctuationKeyCode(keyName);
    }

    private static bool TryGetModifierKeyCode(string keyName, out int code)
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

        if (keyName.Equals("Alt", StringComparison.OrdinalIgnoreCase))
        {
            code = 56;
            return true;
        }

        if (keyName.Equals("AltGr", StringComparison.OrdinalIgnoreCase))
        {
            code = 100;
            return true;
        }

        if (keyName.Equals("Super", StringComparison.OrdinalIgnoreCase) ||
            keyName.Equals("Meta", StringComparison.OrdinalIgnoreCase))
        {
            code = 125;
            return true;
        }

        code = -1;
        return false;
    }

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
            >= 13 and <= 20 => 183 + functionNumber - 13,
            _ => -1,
        };
        return code is not -1;
    }

    public string GetKeyName(int keyCode)
    {
        return _layoutService.GetKeyName(keyCode);
    }

    public bool IsModifierKeyCode(int code)
    {
        return ModifierKeyCodes.Contains(code);
    }

    public int GetKeyCodeForCharacter(char character)
    {
        // Use layout service for proper keyboard layout support
        var result = _layoutService.GetInputForChar(character);
        return result?.KeyCode ?? -1;
    }

    public bool RequiresShift(char character)
    {
        // Use layout service for proper keyboard layout support
        var result = _layoutService.GetInputForChar(character);
        return result?.Shift ?? false;
    }

    /// <summary>
    /// Gets whether a character requires AltGr modifier (for non-US layouts).
    /// </summary>
    public bool RequiresAltGr(char character)
    {
        var result = _layoutService.GetInputForChar(character);
        return result?.AltGr ?? false;
    }

    public char? GetCharacterForKeyCode(int keyCode, bool withShift = false)
    {
        // Use layout service for proper keyboard layout support
        return _layoutService.GetCharFromKeyCode(
            keyCode,
            leftShift: withShift,
            rightShift: false,
            rightAlt: false,
            leftAlt: false,
            leftCtrl: false,
            capsLock: false);
    }

    private static int GetSpecialKeyCode(string keyName)
    {
        return keyName switch
        {
            "Space" => 57,
            "Enter" => 28,
            "Tab" => 15,
            "Backspace" => 14,
            "Escape" or "Esc" => 1,
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

            // Lock keys
            "CapsLock" => 58,
            "NumLock" => 69,
            "ScrollLock" => 70,

            // Special keys
            "PrintScreen" or "PrtSc" => 99,
            "Pause" => 119,

            // Numpad
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
    }

    private static int GetMouseButtonCode(string keyName)
    {
        return keyName switch
        {
            "Mouse Left" => 272,
            "Mouse Right" => 273,
            "Mouse Middle" => 274,
            "Mouse Side" => 275,
            "Mouse Extra" => 276,
            "Mouse Forward" => 277,
            "Mouse Back" => 278,
            "Mouse Task" => 279,
            _ => -1,
        };
    }

    private static int GetLetterKeyCode(char letter)
    {
        return letter switch
        {
            'Q' => 16,
            'W' => 17,
            'E' => 18,
            'R' => 19,
            'T' => 20,
            'Y' => 21,
            'U' => 22,
            'I' => 23,
            'O' => 24,
            'P' => 25,
            'A' => 30,
            'S' => 31,
            'D' => 32,
            'F' => 33,
            'G' => 34,
            'H' => 35,
            'J' => 36,
            'K' => 37,
            'L' => 38,
            'Z' => 44,
            'X' => 45,
            'C' => 46,
            'V' => 47,
            'B' => 48,
            'N' => 49,
            'M' => 50,
            _ => -1,
        };
    }

    private static int GetPunctuationKeyCode(string keyName)
    {
        return keyName switch
        {
            "," => 51,
            "." => 52,
            "-" => 12,
            "=" => 13,
            ";" => 39,
            "'" => 40,
            "[" => 26,
            "]" => 27,
            "\\" => 43,
            "/" => 53,
            "`" => 41,
            _ => -1,
        };
    }
}
