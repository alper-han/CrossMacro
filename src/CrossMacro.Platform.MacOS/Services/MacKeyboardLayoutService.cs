using System;
using System.Collections.Generic;
using CrossMacro.Core.Services;

namespace CrossMacro.Platform.MacOS;

public class MacKeyboardLayoutService : IKeyboardLayoutService
{
    private Dictionary<char, (int KeyCode, bool Shift, bool AltGr)>? _charToInputCache;
    private readonly object _lock = new();

    public string GetKeyName(int keyCode)
    {
        // Use hardcoded fallback (Basic Latin)
        // This is a minimal implementation for macOS to satisfy the interface.
        // A real implementation would use Quartz Event Services to map keycodes.

        // Digits
        if (keyCode == 11) return "0";
        if (keyCode >= 2 && keyCode <= 10) return (keyCode - 1).ToString();

        // Letters
        if (keyCode >= 16 && keyCode <= 25) return "QWERTYUIOP"[keyCode - 16].ToString();
        if (keyCode >= 30 && keyCode <= 38) return "ASDFGHJKL"[keyCode - 30].ToString();
        if (keyCode >= 44 && keyCode <= 50) return "ZXCVBNM"[keyCode - 44].ToString();

        return keyCode switch
        {
            51 => ",",
            52 => ".",
            12 => "-",
            13 => "=",
            39 => ";",
            40 => "'",
            26 => "[",
            27 => "]",
            43 => "\\",
            53 => "/",
            41 => "`",
            57 => "Space",
            28 => "Enter",
            15 => "Tab",
            14 => "Backspace",
            1 => "Escape",
            29 => "Ctrl",
            42 => "Shift",
            56 => "Alt",
            125 => "Command",
            _ => $"Key{keyCode}"
        };
    }

    public int GetKeyCode(string keyName)
    {
        // Minimal reverse mapping
        if (keyName.Length == 1 && char.IsLetter(keyName[0]))
        {
             return char.ToUpper(keyName[0]) switch
             {
                 'Q' => 16, 'W' => 17, 'E' => 18, 'R' => 19, 'T' => 20, 'Y' => 21, 'U' => 22, 'I' => 23, 'O' => 24, 'P' => 25,
                 'A' => 30, 'S' => 31, 'D' => 32, 'F' => 33, 'G' => 34, 'H' => 35, 'J' => 36, 'K' => 37, 'L' => 38,
                 'Z' => 44, 'X' => 45, 'C' => 46, 'V' => 47, 'B' => 48, 'N' => 49, 'M' => 50,
                 _ => -1
             };
        }
        
        return keyName switch
        {
            "Space" => 57,
            "Enter" => 28,
            "Tab" => 15,
            "Backspace" => 14,
            "Escape" => 1,
            "Ctrl" => 29,
            "Shift" => 42,
            "Alt" => 56,
            "Command" or "Super" or "Meta" => 125,
            _ => -1
        };
    }

    public char? GetCharFromKeyCode(int keyCode, bool shift, bool altGr, bool capsLock)
    {
        // Minimal implementation
        return null;
    }

    public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c)
    {
        // Minimal implementation
        // Scan basic range
        lock (_lock)
        {
             if (_charToInputCache == null)
             {
                 _charToInputCache = new Dictionary<char, (int KeyCode, bool Shift, bool AltGr)>();
                 // Populate basics if needed, or leave empty.
                 // For now, let's leave generic logic.
             }
             return null;
        }
    }
}
