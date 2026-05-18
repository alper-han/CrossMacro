using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CrossMacro.Core.Services;
using CrossMacro.Platform.MacOS.Native;
using CrossMacro.Platform.MacOS.Services;

namespace CrossMacro.Platform.MacOS;

public class MacKeyboardLayoutService : IKeyboardLayoutService
{
    private Dictionary<char, (int KeyCode, bool Shift, bool AltGr)>? _charToInputCache;
    private readonly object _lock = new();
    
    // Cache for keyboard layout pointer
    private IntPtr _cachedKeyboardLayout = IntPtr.Zero;
    private IntPtr _cachedInputSource = IntPtr.Zero;

    public string GetKeyName(int keyCode)
    {
        // Try to get character first via UCKeyTranslate
        var c = GetCharFromKeyCode(keyCode, false, false, false, false, false, false);
        if (c.HasValue && !char.IsControl(c.Value))
        {
            return c.Value.ToString().ToUpper();
        }
        
        // Modifier keys
        var modifierName = keyCode switch
        {
            29 => "Ctrl",      // KEY_LEFTCTRL
            97 => "Ctrl",      // KEY_RIGHTCTRL
            42 => "Shift",     // KEY_LEFTSHIFT
            54 => "Shift",     // KEY_RIGHTSHIFT
            56 => "Alt",       // KEY_LEFTALT (Option)
            100 => "Alt",      // KEY_RIGHTALT (Option)
            125 => "Command",  // KEY_LEFTMETA
            126 => "Command",  // KEY_RIGHTMETA
            _ => null
        };
        if (modifierName != null) return modifierName;

        // Special/Navigation keys
        return keyCode switch
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
            127 => "Menu",
            
            // Function Keys
            59 => "F1", 60 => "F2", 61 => "F3", 62 => "F4",
            63 => "F5", 64 => "F6", 65 => "F7", 66 => "F8",
            67 => "F9", 68 => "F10", 87 => "F11", 88 => "F12",
            183 => "F13", 184 => "F14", 185 => "F15", 186 => "F16",
            187 => "F17", 188 => "F18", 189 => "F19", 190 => "F20",
            
            // Numpad
            71 => "Numpad7", 72 => "Numpad8", 73 => "Numpad9", 74 => "Numpad-",
            75 => "Numpad4", 76 => "Numpad5", 77 => "Numpad6", 78 => "Numpad+",
            79 => "Numpad1", 80 => "Numpad2", 81 => "Numpad3",
            82 => "Numpad0", 83 => "Numpad.", 96 => "NumpadEnter",
            98 => "Numpad/", 55 => "Numpad*",
            
            _ => $"Key{keyCode}"
        };
    }

    public int GetKeyCode(string keyName)
    {
        // Modifier keys
        if (keyName.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) return 29;
        if (keyName.Equals("Shift", StringComparison.OrdinalIgnoreCase)) return 42;
        if (keyName.Equals("Alt", StringComparison.OrdinalIgnoreCase) || 
            keyName.Equals("Option", StringComparison.OrdinalIgnoreCase)) return 56;
        if (keyName.Equals("Command", StringComparison.OrdinalIgnoreCase) || 
            keyName.Equals("Super", StringComparison.OrdinalIgnoreCase)) return 125;

        // Function keys
        if (keyName.StartsWith("F", StringComparison.OrdinalIgnoreCase) && 
            int.TryParse(keyName[1..], out var fNum))
        {
            if (fNum >= 1 && fNum <= 10) return 59 + fNum - 1;
            if (fNum == 11) return 87;
            if (fNum == 12) return 88;
            if (fNum >= 13 && fNum <= 20) return 183 + fNum - 13;
        }

        // Special keys
        var special = keyName switch
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
            "Menu" => 127,
            _ => -1
        };
        if (special != -1) return special;

        // Try to find by character
        if (keyName.Length == 1)
        {
            var input = GetInputForChar(keyName[0]);
            if (input.HasValue) return input.Value.KeyCode;
        }

        return -1;
    }

    public char? GetCharFromKeyCode(int keyCode, bool leftShift, bool rightShift, bool rightAlt, bool leftAlt, bool leftCtrl, bool capsLock)
    {
        bool shift = leftShift || rightShift;
        bool option = leftAlt || rightAlt; // Option key on Mac
        
        // Don't produce chars for modifiers
        if (IsModifier(keyCode)) return null;
        
        // Space special case
        if (keyCode == 57) return ' ';

        try
        {
            // Convert evdev code to Mac key code
            ushort macKeyCode = KeyMap.ToMacKey(keyCode);
            if (macKeyCode == 0xFFFF) return null;
            
            // Get keyboard layout
            IntPtr layoutData = GetKeyboardLayoutData();
            if (layoutData == IntPtr.Zero) return null;
            
            // Build modifier state for UCKeyTranslate
            // Mac modifier format: bits for shift, option, control, command
            uint modifierState = 0;
            if (capsLock) modifierState |= 1u << 10; // alphaLock
            if (shift) modifierState |= 1u << 9; // shiftKey
            if (option) modifierState |= 1u << 11; // optionKey
            if (leftCtrl) modifierState |= 1u << 12; // controlKey
            
            // Shift modifier state to match UCKeyTranslate format (>> 8)
            modifierState = (modifierState >> 8) & 0xFF;
            
            uint deadKeyState = 0;
            ushort[] output = new ushort[4];
            nuint actualLength;
            
            int result = CoreGraphics.UCKeyTranslate(
                layoutData,
                macKeyCode,
                CoreGraphics.kUCKeyActionDown,
                modifierState,
                CoreGraphics.LMGetKbdType(),
                CoreGraphics.kUCKeyTranslateNoDeadKeysMask,
                ref deadKeyState,
                (nuint)output.Length,
                out actualLength,
                output);
            
            char translated = (char)output[0];
            if (result == 0 && actualLength > 0 && !char.IsControl(translated))
            {
                return translated;
            }
        }
        catch
        {
            // Fall through to fallback
        }
        
        return null;
    }

    public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c)
    {
        lock (_lock)
        {
            if (_charToInputCache == null)
            {
                BuildCharInputCache();
            }

            return _charToInputCache!.TryGetValue(c, out var input) ? input : null;
        }
    }

    private void BuildCharInputCache()
    {
        _charToInputCache = new Dictionary<char, (int KeyCode, bool Shift, bool AltGr)>();

        // Scan all key codes with different modifiers
        for (int code = 1; code < 128; code++)
        {
            if (IsModifier(code)) continue;

            // No modifiers
            TryAddCharToCache(code, false, false);
            // Shift
            TryAddCharToCache(code, true, false);
            // Option (AltGr equivalent on Mac)
            TryAddCharToCache(code, false, true);
            // Shift + Option
            TryAddCharToCache(code, true, true);
        }
    }

    private void TryAddCharToCache(int code, bool shift, bool option)
    {
        var c = GetCharFromKeyCode(code, shift, false, option, false, false, false);
        if (c.HasValue && !_charToInputCache!.ContainsKey(c.Value))
        {
            _charToInputCache[c.Value] = (code, shift, option);
        }
    }

    private IntPtr GetKeyboardLayoutData()
    {
        if (_cachedKeyboardLayout != IntPtr.Zero)
            return _cachedKeyboardLayout;

        try
        {
            // Get current keyboard input source
            IntPtr inputSource = CoreGraphics.TISCopyCurrentKeyboardLayoutInputSource();
            if (inputSource == IntPtr.Zero)
            {
                inputSource = CoreGraphics.TISCopyCurrentKeyboardInputSource();
            }
            
            if (inputSource == IntPtr.Zero) return IntPtr.Zero;
            
            _cachedInputSource = inputSource;
            
            // Get the property key for keyboard layout data
            IntPtr propertyKey = CoreGraphics.kTISPropertyUnicodeKeyLayoutData;
            if (propertyKey == IntPtr.Zero) return IntPtr.Zero;
            
            // Get the layout data
            IntPtr layoutData = CoreGraphics.TISGetInputSourceProperty(inputSource, propertyKey);
            if (layoutData == IntPtr.Zero) return IntPtr.Zero;
            
            // Get the actual byte pointer from CFData
            _cachedKeyboardLayout = CoreFoundation.CFDataGetBytePtr(layoutData);
            
            return _cachedKeyboardLayout;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static bool IsModifier(int keyCode)
    {
        return keyCode is 29 or 97 or 42 or 54 or 56 or 100 or 125 or 126;
    }
}
