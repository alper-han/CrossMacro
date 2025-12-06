using System;
using System.Collections.Generic;

namespace CrossMacro.Infrastructure.Helpers;

/// <summary>
/// Helper to map between Evdev keycodes and characters (US QWERTY Layout)
/// </summary>
public static class KeyMapHelper
{
    // Maps Char -> (KeyCode, Shift, AltGr)
    private static readonly Dictionary<char, (int KeyCode, bool Shift, bool AltGr)> _charToKey = new();
    
    // Maps KeyCode -> (Normal, Shift, AltGr) - used for input monitoring (reverse lookup)
    // For monitoring, we mainly care about detecting typed chars.
    // We only track Normal and Shift for 'typing detection' usually, but let's keep it simple.
    // Actually, EvdevReader gives us keys. We map Key+Shift -> Char.
    // If user types AltGr+Q, we should detect '@'.
    // So we need: Dictionary<(int KeyCode, bool Shift, bool AltGr), char>
    
    private static readonly Dictionary<(int KeyCode, bool Shift, bool AltGr), char> _keyToChar = new();

    static KeyMapHelper()
    {
        // Define mappings: KeyCode, Normal, Shift, AltGr
        // Turkish Q Layout
        
        // KeyRow Numbers
        // Key 41 (`/~ in US) -> " é <
        AddMapping(41, '"', 'é', '<'); 
        
        AddMapping(2, '1', '!', '>'); // 1 ! > (AltGr)
        AddMapping(3, '2', '\'', '£'); // 2 ' £
        AddMapping(4, '3', '^', '#'); // 3 ^ #
        AddMapping(5, '4', '+', '$'); // 4 + $
        AddMapping(6, '5', '%', '½'); // 5 % ½
        AddMapping(7, '6', '&', Char.MinValue); // 6 & (No AltGr usually)
        AddMapping(8, '7', '/', '{'); // 7 / {
        AddMapping(9, '8', '(', '['); // 8 ( [
        AddMapping(10, '9', ')', ']'); // 9 ) ]
        AddMapping(11, '0', '=', '}'); // 0 = }
        AddMapping(12, '*', '?', '\\'); // * ? \
        AddMapping(13, '-', '_', '|'); // - _ |

        // Row Q
        AddMapping(16, 'q', 'Q', '@'); // q Q @
        AddMapping(17, 'w', 'W', Char.MinValue);
        AddMapping(18, 'e', 'E', '€'); // e E €
        AddMapping(19, 'r', 'R', Char.MinValue);
        AddMapping(20, 't', 'T', '₺'); // t T ₺
        AddMapping(21, 'y', 'Y', Char.MinValue);
        AddMapping(22, 'u', 'U', Char.MinValue);
        AddMapping(23, 'ı', 'I', Char.MinValue); // ı I
        AddMapping(24, 'o', 'O', Char.MinValue);
        AddMapping(25, 'p', 'P', Char.MinValue);
        AddMapping(26, 'ğ', 'Ğ', Char.MinValue); // ğ Ğ
        AddMapping(27, 'ü', 'Ü', '~'); // ü Ü ~

        // Row A
        AddMapping(30, 'a', 'A', 'æ'); // a A æ
        AddMapping(31, 's', 'S', 'ß'); // s S ß
        AddMapping(32, 'd', 'D', Char.MinValue);
        AddMapping(33, 'f', 'F', Char.MinValue);
        AddMapping(34, 'g', 'G', Char.MinValue);
        AddMapping(35, 'h', 'H', Char.MinValue);
        AddMapping(36, 'j', 'J', Char.MinValue);
        AddMapping(37, 'k', 'K', Char.MinValue);
        AddMapping(38, 'l', 'L', Char.MinValue);
        AddMapping(39, 'ş', 'Ş', '´'); // ş Ş ´ (Dead key? treating as char for now)
        AddMapping(40, 'i', 'İ', Char.MinValue); // i İ
        AddMapping(43, ',', ';', '`'); // , ; `

        // Row Z
        AddMapping(44, 'z', 'Z', Char.MinValue);
        AddMapping(45, 'x', 'X', Char.MinValue);
        AddMapping(46, 'c', 'C', Char.MinValue);
        AddMapping(47, 'v', 'V', Char.MinValue);
        AddMapping(48, 'b', 'B', Char.MinValue);
        AddMapping(49, 'n', 'N', Char.MinValue);
        AddMapping(50, 'm', 'M', Char.MinValue);
        AddMapping(51, 'ö', 'Ö', Char.MinValue); // ö Ö
        AddMapping(52, 'ç', 'Ç', Char.MinValue); // ç Ç
        AddMapping(53, '.', ':', Char.MinValue); // . :

        // Space
        AddMapping(57, ' ', ' ', Char.MinValue);
    }

    private static void AddMapping(int keyCode, char normal, char shift, char altGr)
    {
        // Forward map for Detection
        if (normal != Char.MinValue) _keyToChar[(keyCode, false, false)] = normal;
        if (shift != Char.MinValue) _keyToChar[(keyCode, true, false)] = shift;
        if (altGr != Char.MinValue) _keyToChar[(keyCode, false, true)] = altGr;
        // Shift+AltGr is rarely used in typical expansions, ignoring for now

        // Reverse map for Typing
        if (normal != Char.MinValue && !_charToKey.ContainsKey(normal))
            _charToKey[normal] = (keyCode, false, false);
            
        if (shift != Char.MinValue && !_charToKey.ContainsKey(shift))
            _charToKey[shift] = (keyCode, true, false);
            
        if (altGr != Char.MinValue && !_charToKey.ContainsKey(altGr))
            _charToKey[altGr] = (keyCode, false, true);
    }

    public static char? MapKeyCodeToChar(int keyCode, bool shift, bool altGr)
    {
        // Try precise match
        if (_keyToChar.TryGetValue((keyCode, shift, altGr), out var c))
        {
            return c;
        }
        
        // Fallbacks
        // If AltGr is pressed but no mapping exists, maybe return null or normal char?
        // Usually null, as it doesn't produce text we know.
        
        return null;
    }

    public static (int KeyCode, bool Shift, bool AltGr)? MapCharToKeyCode(char c)
    {
        if (_charToKey.TryGetValue(c, out var mapping))
        {
            return mapping;
        }
        return null;
    }
}
