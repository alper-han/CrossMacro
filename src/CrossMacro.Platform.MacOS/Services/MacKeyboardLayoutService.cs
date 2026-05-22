using System;
using System.Collections.Generic;
using System.Threading;
using CrossMacro.Core.Services;
using CrossMacro.Platform.MacOS.Native;
using CrossMacro.Platform.MacOS.Services;

namespace CrossMacro.Platform.MacOS;

public class MacKeyboardLayoutService : IKeyboardLayoutService, IDisposable
{
    private Dictionary<char, (int KeyCode, bool Shift, bool AltGr)>? _charToInputCache;
    private readonly object _lock = new();
    private readonly object _layoutLock = new();
    private readonly Func<bool> _isMainThread;
    private readonly SynchronizationContext? _mainThreadContext;
    private readonly Func<(IntPtr LayoutData, IntPtr KeyboardLayout, byte KeyboardType)> _loadKeyboardLayoutData;
    
    // Cache for keyboard layout pointer
    private IntPtr _cachedKeyboardLayout = IntPtr.Zero;
    private IntPtr _cachedLayoutData = IntPtr.Zero;
    private byte _cachedKeyboardType;
    private bool _disposed;

    public MacKeyboardLayoutService()
        : this(
            MacOSMainThread.IsMainThread,
            SynchronizationContext.Current,
            LoadNativeKeyboardLayoutData,
            warmOnConstruction: true)
    {
    }

    internal MacKeyboardLayoutService(
        Func<bool> isMainThread,
        SynchronizationContext? mainThreadContext,
        Func<(IntPtr LayoutData, IntPtr KeyboardLayout, byte KeyboardType)> loadKeyboardLayoutData,
        bool warmOnConstruction)
    {
        _isMainThread = isMainThread;
        _mainThreadContext = mainThreadContext;
        _loadKeyboardLayoutData = loadKeyboardLayoutData;

        if (warmOnConstruction && _isMainThread())
        {
            LoadAndCacheKeyboardLayoutData();
        }
    }

    public string GetKeyName(int keyCode)
    {
        var semanticName = GetSemanticKeyName(keyCode);
        if (semanticName != null) return semanticName;

        // Try to get character first via UCKeyTranslate
        var c = GetCharFromKeyCode(keyCode, false, false, false, false, false, false);
        if (c.HasValue && !char.IsControl(c.Value))
        {
            return c.Value.ToString().ToUpper();
        }

        return $"Key{keyCode}";
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
            "NumLock" => 69,
            "Help" => 138,
            "Mute" => 113,
            "VolumeDown" => 114,
            "VolumeUp" => 115,
            "PlayPause" => 164,
            "PreviousSong" => 165,
            "NextSong" => 163,
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

    private static string? GetSemanticKeyName(int keyCode)
    {
        return keyCode switch
        {
            29 => "Ctrl",
            97 => "Ctrl",
            42 => "Shift",
            54 => "Shift",
            56 => "Alt",
            100 => "Alt",
            125 => "Command",
            126 => "Command",

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
            113 => "Mute",
            114 => "VolumeDown",
            115 => "VolumeUp",
            164 => "PlayPause",
            165 => "PreviousSong",
            163 => "NextSong",
            86 => "ISOSection",
            124 => "Yen",
            95 => "NumpadJpComma",

            59 => "F1", 60 => "F2", 61 => "F3", 62 => "F4",
            63 => "F5", 64 => "F6", 65 => "F7", 66 => "F8",
            67 => "F9", 68 => "F10", 87 => "F11", 88 => "F12",
            183 => "F13", 184 => "F14", 185 => "F15", 186 => "F16",
            187 => "F17", 188 => "F18", 189 => "F19", 190 => "F20",

            71 => "Numpad7", 72 => "Numpad8", 73 => "Numpad9", 74 => "Numpad-",
            75 => "Numpad4", 76 => "Numpad5", 77 => "Numpad6", 78 => "NumpadPlus",
            79 => "Numpad1", 80 => "Numpad2", 81 => "Numpad3",
            82 => "Numpad0", 83 => "Numpad.", 96 => "NumpadEnter",
            98 => "Numpad/", 55 => "Numpad*", 117 => "Numpad=",

            _ => null
        };
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
            if (GetKeyboardLayoutData() == IntPtr.Zero) return null;
            
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
            
            int result;
            lock (_layoutLock)
            {
                if (_disposed || _cachedKeyboardLayout == IntPtr.Zero)
                {
                    return null;
                }

                result = CoreGraphics.UCKeyTranslate(
                    _cachedKeyboardLayout,
                    macKeyCode,
                    CoreGraphics.kUCKeyActionDown,
                    modifierState,
                    _cachedKeyboardType,
                    CoreGraphics.kUCKeyTranslateNoDeadKeysMask,
                    ref deadKeyState,
                    (nuint)output.Length,
                    out actualLength,
                    output);
            }
            
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
            if (_charToInputCache == null && !BuildCharInputCache())
            {
                return null;
            }

            return _charToInputCache!.TryGetValue(c, out var input) ? input : null;
        }
    }

    private bool BuildCharInputCache()
    {
        if (GetKeyboardLayoutData() == IntPtr.Zero)
        {
            return false;
        }

        var charToInputCache = new Dictionary<char, (int KeyCode, bool Shift, bool AltGr)>();

        // Scan all key codes with different modifiers
        for (int code = 1; code < 128; code++)
        {
            if (IsModifier(code)) continue;

            // No modifiers
            TryAddCharToCache(charToInputCache, code, false, false);
            // Shift
            TryAddCharToCache(charToInputCache, code, true, false);
            // Option (AltGr equivalent on Mac)
            TryAddCharToCache(charToInputCache, code, false, true);
            // Shift + Option
            TryAddCharToCache(charToInputCache, code, true, true);
        }

        _charToInputCache = charToInputCache;
        return true;
    }

    private void TryAddCharToCache(Dictionary<char, (int KeyCode, bool Shift, bool AltGr)> charToInputCache, int code, bool shift, bool option)
    {
        var c = GetCharFromKeyCode(code, shift, false, option, false, false, false);
        if (c.HasValue && !charToInputCache.ContainsKey(c.Value))
        {
            charToInputCache[c.Value] = (code, shift, option);
        }
    }

    private IntPtr GetKeyboardLayoutData()
    {
        lock (_layoutLock)
        {
            if (_disposed)
            {
                return IntPtr.Zero;
            }

            if (_cachedKeyboardLayout != IntPtr.Zero)
            {
                return _cachedKeyboardLayout;
            }
        }

        if (_isMainThread())
        {
            return LoadAndCacheKeyboardLayoutData();
        }

        if (_mainThreadContext == null)
        {
            return IntPtr.Zero;
        }

        IntPtr layoutData = IntPtr.Zero;
        try
        {
            _mainThreadContext.Send(_ => layoutData = LoadAndCacheKeyboardLayoutData(), null);
        }
        catch
        {
            return IntPtr.Zero;
        }

        return layoutData;
    }

    private IntPtr LoadAndCacheKeyboardLayoutData()
    {
        lock (_layoutLock)
        {
            if (_disposed)
            {
                return IntPtr.Zero;
            }

            if (_cachedKeyboardLayout != IntPtr.Zero)
            {
                return _cachedKeyboardLayout;
            }

            if (!_isMainThread())
            {
                return IntPtr.Zero;
            }

            var layoutData = _loadKeyboardLayoutData();
            _cachedLayoutData = layoutData.LayoutData;
            _cachedKeyboardLayout = layoutData.KeyboardLayout;
            _cachedKeyboardType = layoutData.KeyboardType;
            return _cachedKeyboardLayout;
        }
    }

    private static (IntPtr LayoutData, IntPtr KeyboardLayout, byte KeyboardType) LoadNativeKeyboardLayoutData()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return default;
        }

        IntPtr inputSource = IntPtr.Zero;
        IntPtr retainedLayoutData = IntPtr.Zero;

        try
        {
            // Get current keyboard input source
            inputSource = CoreGraphics.TISCopyCurrentKeyboardLayoutInputSource();
            if (inputSource == IntPtr.Zero)
            {
                inputSource = CoreGraphics.TISCopyCurrentKeyboardInputSource();
            }
            
            if (inputSource == IntPtr.Zero) return default;

            // Get the property key for keyboard layout data
            IntPtr propertyKey = CoreGraphics.kTISPropertyUnicodeKeyLayoutData;
            if (propertyKey == IntPtr.Zero)
            {
                ReleaseInputSource(ref inputSource);
                return default;
            }
            
            // Get the layout data
            IntPtr layoutData = CoreGraphics.TISGetInputSourceProperty(inputSource, propertyKey);
            if (layoutData == IntPtr.Zero)
            {
                ReleaseInputSource(ref inputSource);
                return default;
            }

            retainedLayoutData = CoreFoundation.CFRetain(layoutData);
            if (retainedLayoutData == IntPtr.Zero)
            {
                ReleaseInputSource(ref inputSource);
                return default;
            }
            
            // Get the actual byte pointer from CFData
            var keyboardLayout = CoreFoundation.CFDataGetBytePtr(retainedLayoutData);
            if (keyboardLayout == IntPtr.Zero)
            {
                ReleaseLayoutData(ref retainedLayoutData);
                ReleaseInputSource(ref inputSource);
                return default;
            }

            var keyboardType = CoreGraphics.LMGetKbdType();
            ReleaseInputSource(ref inputSource);

            return (retainedLayoutData, keyboardLayout, keyboardType);
        }
        catch
        {
            if (retainedLayoutData != IntPtr.Zero)
            {
                ReleaseLayoutData(ref retainedLayoutData);
            }

            if (inputSource != IntPtr.Zero)
            {
                ReleaseInputSource(ref inputSource);
            }

            return default;
        }
    }

    private static void ReleaseInputSource(ref IntPtr inputSource)
    {
        if (inputSource == IntPtr.Zero)
        {
            return;
        }

        var sourceToRelease = inputSource;
        inputSource = IntPtr.Zero;
        CoreFoundation.CFRelease(sourceToRelease);
    }

    private static void ReleaseLayoutData(ref IntPtr layoutData)
    {
        if (layoutData == IntPtr.Zero)
        {
            return;
        }

        var dataToRelease = layoutData;
        layoutData = IntPtr.Zero;
        CoreFoundation.CFRelease(dataToRelease);
    }

    private static bool IsModifier(int keyCode)
    {
        return keyCode is 29 or 97 or 42 or 54 or 56 or 100 or 125 or 126;
    }

    public void Dispose()
    {
        lock (_layoutLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_cachedLayoutData != IntPtr.Zero && OperatingSystem.IsMacOS())
            {
                CoreFoundation.CFRelease(_cachedLayoutData);
            }

            _cachedLayoutData = IntPtr.Zero;
            _cachedKeyboardLayout = IntPtr.Zero;
            _cachedKeyboardType = 0;
        }

        GC.SuppressFinalize(this);
    }
}
