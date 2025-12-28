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
            97 => "Ctrl",      // Right Ctrl
            42 => "Shift",
            54 => "Shift",     // Right Shift
            56 => "Alt",
            100 => "Alt",      // Right Alt
            125 => "Command",
            126 => "Command",  // Right Command
            
            // Navigation
            103 => "Up",
            108 => "Down",
            105 => "Left",
            106 => "Right",
            104 => "PageUp",
            109 => "PageDown",
            102 => "Home",
            107 => "End",
            110 => "Insert",
            111 => "Delete",
            
            // Function Keys
            59 => "F1", 60 => "F2", 61 => "F3", 62 => "F4", 
            63 => "F5", 64 => "F6", 65 => "F7", 66 => "F8", 
            67 => "F9", 68 => "F10", 87 => "F11", 88 => "F12",
            
            // Locks & Special
            58 => "CapsLock",
            69 => "NumLock",
            70 => "ScrollLock",
            99 => "PrintScreen",
            119 => "Pause",
            
            // Numpad
            71 => "Numpad7", 72 => "Numpad8", 73 => "Numpad9", 74 => "Numpad-",
            75 => "Numpad4", 76 => "Numpad5", 77 => "Numpad6", 78 => "Numpad+",
            79 => "Numpad1", 80 => "Numpad2", 81 => "Numpad3",
            82 => "Numpad0", 83 => "Numpad.", 96 => "NumpadEnter",
             
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

    public char? GetCharFromKeyCode(int keyCode, bool leftShift, bool rightShift, bool rightAlt, bool leftAlt, bool leftCtrl, bool capsLock)
    {
        // Minimal implementation
        // For now, map simple keys if feasible or return null
        return null; // TODO: Implement macOS Quartz event mapping
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
