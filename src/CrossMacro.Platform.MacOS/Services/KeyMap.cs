using System.Collections.Generic;
using CrossMacro.Core.Services;

namespace CrossMacro.Platform.MacOS.Services;

internal static class KeyMap
{
    public static ushort ToMacKey(int code)
    {
        if (_toMac.TryGetValue(code, out var vk))
            return vk;
        return 0xFFFF;
    }

    public static int FromMacKey(ushort code)
    {
        if (_fromMac.TryGetValue(code, out var c))
            return c;
        return 0; 
    }

    private static readonly Dictionary<int, ushort> _toMac = new()
    {
        { InputEventCode.KEY_A, 0x00 },
        { InputEventCode.KEY_S, 0x01 },
        { InputEventCode.KEY_D, 0x02 },
        { InputEventCode.KEY_F, 0x03 },
        { InputEventCode.KEY_H, 0x04 },
        { InputEventCode.KEY_G, 0x05 },
        { InputEventCode.KEY_Z, 0x06 },
        { InputEventCode.KEY_X, 0x07 },
        { InputEventCode.KEY_C, 0x08 },
        { InputEventCode.KEY_V, 0x09 },
        { InputEventCode.KEY_B, 0x0B },
        { InputEventCode.KEY_Q, 0x0C },
        { InputEventCode.KEY_W, 0x0D },
        { InputEventCode.KEY_E, 0x0E },
        { InputEventCode.KEY_R, 0x0F },
        { InputEventCode.KEY_Y, 0x10 },
        { InputEventCode.KEY_T, 0x11 },
        { InputEventCode.KEY_1, 0x12 },
        { InputEventCode.KEY_2, 0x13 },
        { InputEventCode.KEY_3, 0x14 },
        { InputEventCode.KEY_4, 0x15 },
        { InputEventCode.KEY_6, 0x16 },
        { InputEventCode.KEY_5, 0x17 },
        { InputEventCode.KEY_EQUAL, 0x18 },
        { InputEventCode.KEY_9, 0x19 },
        { InputEventCode.KEY_7, 0x1A },
        { InputEventCode.KEY_MINUS, 0x1B },
        { InputEventCode.KEY_8, 0x1C },
        { InputEventCode.KEY_0, 0x1D },
        { InputEventCode.KEY_RIGHTBRACE, 0x1E },
        { InputEventCode.KEY_O, 0x1F },
        { InputEventCode.KEY_U, 0x20 },
        { InputEventCode.KEY_LEFTBRACE, 0x21 },
        { InputEventCode.KEY_I, 0x22 },
        { InputEventCode.KEY_P, 0x23 },
        { InputEventCode.KEY_ENTER, 0x24 },
        { InputEventCode.KEY_L, 0x25 },
        { InputEventCode.KEY_J, 0x26 },
        { InputEventCode.KEY_APOSTROPHE, 0x27 },
        { InputEventCode.KEY_K, 0x28 },
        { InputEventCode.KEY_SEMICOLON, 0x29 },
        { InputEventCode.KEY_BACKSLASH, 0x2A },
        { InputEventCode.KEY_COMMA, 0x2B },
        { InputEventCode.KEY_SLASH, 0x2C },
        { InputEventCode.KEY_N, 0x2D },
        { InputEventCode.KEY_M, 0x2E },
        { InputEventCode.KEY_DOT, 0x2F },
        { InputEventCode.KEY_TAB, 0x30 },
        { InputEventCode.KEY_SPACE, 0x31 },
        { InputEventCode.KEY_GRAVE, 0x32 },
        { InputEventCode.KEY_BACKSPACE, 0x33 },
        { InputEventCode.KEY_ESC, 0x35 },
        { InputEventCode.KEY_LEFTMETA, 0x37 }, 
        { InputEventCode.KEY_LEFTSHIFT, 0x38 },
        { InputEventCode.KEY_CAPSLOCK, 0x39 },
        { InputEventCode.KEY_LEFTALT, 0x3A }, 
        { InputEventCode.KEY_LEFTCTRL, 0x3B },
        { InputEventCode.KEY_RIGHTSHIFT, 0x3C },
        { InputEventCode.KEY_RIGHTALT, 0x3D }, 
        { InputEventCode.KEY_RIGHTCTRL, 0x3E },
        { InputEventCode.KEY_RIGHTMETA, 0x36 }, 
        
        { InputEventCode.KEY_F5, 0x60 },
        { InputEventCode.KEY_F6, 0x61 },
        { InputEventCode.KEY_F7, 0x62 },
        { InputEventCode.KEY_F3, 0x63 },
        { InputEventCode.KEY_F8, 0x64 },
        { InputEventCode.KEY_F9, 0x65 },
        { InputEventCode.KEY_F11, 0x67 },
        { InputEventCode.KEY_F10, 0x6D },
        { InputEventCode.KEY_F12, 0x6F },

        { InputEventCode.KEY_HOME, 0x73 },
        { InputEventCode.KEY_PAGEUP, 0x74 },
        { InputEventCode.KEY_DELETE, 0x75 },
        { InputEventCode.KEY_F4, 0x76 },
        { InputEventCode.KEY_END, 0x77 },
        { InputEventCode.KEY_F2, 0x78 },
        { InputEventCode.KEY_PAGEDOWN, 0x79 },
        { InputEventCode.KEY_F1, 0x7A },
        { InputEventCode.KEY_LEFT, 0x7B },
        { InputEventCode.KEY_RIGHT, 0x7C },
        { InputEventCode.KEY_DOWN, 0x7D },
        { InputEventCode.KEY_UP, 0x7E },
        
        // Numpad
        { InputEventCode.KEY_KP0, 0x52 },
        { InputEventCode.KEY_KP1, 0x53 },
        { InputEventCode.KEY_KP2, 0x54 },
        { InputEventCode.KEY_KP3, 0x55 },
        { InputEventCode.KEY_KP4, 0x56 },
        { InputEventCode.KEY_KP5, 0x57 },
        { InputEventCode.KEY_KP6, 0x58 },
        { InputEventCode.KEY_KP7, 0x59 },
        { InputEventCode.KEY_KP8, 0x5B },
        { InputEventCode.KEY_KP9, 0x5C },
        { InputEventCode.KEY_KPDOT, 0x41 },
        { InputEventCode.KEY_KPASTERISK, 0x43 },
        { InputEventCode.KEY_KPPLUS, 0x45 },
        { InputEventCode.KEY_KPMINUS, 0x4E },
        { InputEventCode.KEY_KPENTER, 0x4C },
        { InputEventCode.KEY_KPSLASH, 0x4B },
        { InputEventCode.KEY_KPEQUAL, 0x51 },
        { InputEventCode.KEY_NUMLOCK, 0x47 }, // Clear/NumLock
        
        // Extended Function Keys
        { InputEventCode.KEY_F13, 0x69 },
        { InputEventCode.KEY_F14, 0x6B },
        { InputEventCode.KEY_F15, 0x71 },
        { InputEventCode.KEY_F16, 0x6A },
        { InputEventCode.KEY_F17, 0x40 },
        { InputEventCode.KEY_F18, 0x4F },
        { InputEventCode.KEY_F19, 0x50 },
        { InputEventCode.KEY_F20, 0x5A },
    };
    
    private static readonly Dictionary<ushort, int> _fromMac = new();

    static KeyMap()
    {
        foreach (var kvp in _toMac)
        {
            if (!_fromMac.ContainsKey(kvp.Value))
            {
                _fromMac.Add(kvp.Value, kvp.Key);
            }
        }
    }
}
