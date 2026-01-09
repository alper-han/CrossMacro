using CrossMacro.Core.Services;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Maps between key names and key codes.
/// Supports keyboard layout-aware key name resolution.
/// </summary>
public class KeyCodeMapper : IKeyCodeMapper
{
    private readonly IKeyboardLayoutService _layoutService;
    
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
        126  // Right Super
    };
    
    public KeyCodeMapper(IKeyboardLayoutService layoutService)
    {
        _layoutService = layoutService;
    }
    
    public int GetKeyCode(string keyName)
    {
        // Modifier keys
        if (keyName.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
            return 29; 
        if (keyName.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            return 42; 
        if (keyName.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            return 56; 
        if (keyName.Equals("AltGr", StringComparison.OrdinalIgnoreCase))
            return 100; 
        if (keyName.Equals("Super", StringComparison.OrdinalIgnoreCase) || 
            keyName.Equals("Meta", StringComparison.OrdinalIgnoreCase))
            return 125; 

        // Function keys (F1-F24)
        if (keyName.StartsWith("F", StringComparison.OrdinalIgnoreCase) && 
            int.TryParse(keyName[1..], out var fNum))
        {
            if (fNum >= 1 && fNum <= 24)
                return 59 + fNum - 1; 
        }

        // Special keys
        var special = GetSpecialKeyCode(keyName);
        if (special != -1) return special;

        // Mouse buttons
        var mouseCode = GetMouseButtonCode(keyName);
        if (mouseCode != -1) return mouseCode;

        // Try layout service first
        var code = _layoutService.GetKeyCode(keyName);
        if (code != -1) return code;

        // Letter keys (QWERTY layout fallback)
        if (keyName.Length == 1 && char.IsLetter(keyName[0]))
        {
            return GetLetterKeyCode(char.ToUpper(keyName[0]));
        }

        // Digit keys
        if (keyName.Length == 1 && char.IsDigit(keyName[0]))
        {
            var digit = keyName[0] - '0';
            return digit == 0 ? 11 : 2 + digit - 1; 
        }

        // Punctuation
        return GetPunctuationKeyCode(keyName);
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
            "Numpad7" => 71, "Numpad8" => 72, "Numpad9" => 73, "Numpad-" => 74,
            "Numpad4" => 75, "Numpad5" => 76, "Numpad6" => 77, "Numpad+" => 78,
            "Numpad1" => 79, "Numpad2" => 80, "Numpad3" => 81,
            "Numpad0" => 82, "Numpad." => 83, "NumpadEnter" => 96, "Numpad/" => 98,
            "Numpad*" => 55, "Numpad=" => 117,
            
            _ => -1
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
            _ => -1
        };
    }
    
    private static int GetLetterKeyCode(char letter)
    {
        return letter switch
        {
            'Q' => 16, 'W' => 17, 'E' => 18, 'R' => 19, 'T' => 20, 
            'Y' => 21, 'U' => 22, 'I' => 23, 'O' => 24, 'P' => 25,
            'A' => 30, 'S' => 31, 'D' => 32, 'F' => 33, 'G' => 34, 
            'H' => 35, 'J' => 36, 'K' => 37, 'L' => 38,
            'Z' => 44, 'X' => 45, 'C' => 46, 'V' => 47, 'B' => 48, 
            'N' => 49, 'M' => 50,
            _ => -1
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
            _ => -1
        };
    }
}

