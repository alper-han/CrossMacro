using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Windows.Helpers;
using CrossMacro.Platform.Windows.Native;

namespace CrossMacro.Platform.Windows.Services;

public class WindowsKeyboardLayoutService : IKeyboardLayoutService
{
    // Backlog: D3-PLAT-001 in docs/platform-borclari-backlog.md.
    // Accuracy for non-US and AltGr-heavy layouts still requires broader validation coverage.
    private static class Vk
    {
        public const ushort Pause = 0x13;
        public const ushort PrintScreen = 0x2C;
        public const ushort NumLock = 0x90;
        public const ushort ScrollLock = 0x91;
        public const ushort CapsLock = 0x14;

        public const ushort LeftShift = 0xA0;
        public const ushort Shift = 0x10;
        public const ushort RightShift = 0xA1;
        public const ushort LeftCtrl = 0xA2;
        public const ushort Ctrl = 0x11;
        public const ushort RightCtrl = 0xA3;
        public const ushort LeftAlt = 0xA4;
        public const ushort Alt = 0x12;
        public const ushort RightAlt = 0xA5;
        public const ushort LeftWin = 0x5B;
        public const ushort RightWin = 0x5C;
        public const ushort Menu = 0x5D;

        public const ushort VolumeMute = 0xAD;
        public const ushort VolumeDown = 0xAE;
        public const ushort VolumeUp = 0xAF;
        public const ushort MediaNext = 0xB0;
        public const ushort MediaPrev = 0xB1;
        public const ushort MediaStop = 0xB2;
        public const ushort PlayPause = 0xB3;

        public const ushort PageUp = 0x21;
        public const ushort Delete = 0x2E;
    }

    private static readonly Dictionary<ushort, string> SpecialKeyNames = new()
    {
        [Vk.Pause] = "Pause",
        [Vk.PrintScreen] = "PrintScreen",
        [Vk.NumLock] = "NumLock",
        [Vk.ScrollLock] = "ScrollLock",
        [Vk.CapsLock] = "CapsLock"
    };

    private static readonly Dictionary<ushort, string> ModifierKeyNames = new()
    {
        [Vk.LeftShift] = "LeftShift",
        [Vk.Shift] = "LeftShift",
        [Vk.RightShift] = "RightShift",
        [Vk.LeftCtrl] = "LeftCtrl",
        [Vk.Ctrl] = "LeftCtrl",
        [Vk.RightCtrl] = "RightCtrl",
        [Vk.LeftAlt] = "LeftAlt",
        [Vk.Alt] = "LeftAlt",
        [Vk.RightAlt] = "RightAlt",
        [Vk.LeftWin] = "LeftWin",
        [Vk.RightWin] = "RightWin",
        [Vk.Menu] = "Menu"
    };

    private static readonly Dictionary<ushort, string> MediaKeyNames = new()
    {
        [Vk.VolumeMute] = "VolumeMute",
        [Vk.VolumeDown] = "VolumeDown",
        [Vk.VolumeUp] = "VolumeUp",
        [Vk.PlayPause] = "PlayPause",
        [Vk.MediaNext] = "MediaNext",
        [Vk.MediaPrev] = "MediaPrev",
        [Vk.MediaStop] = "MediaStop"
    };

    private const int ScanCodeToLParamShift = 16;
    private const int ExtendedKeyMask = 1 << 24;
    private const int KeyNameBufferSize = 256;

    public string GetKeyName(int keyCode)
    {
        ushort vk = WindowsKeyMap.GetVirtualKey(keyCode);
        if (vk == 0) return $"Key_{keyCode}";

        if (SpecialKeyNames.TryGetValue(vk, out var specialName)) return specialName;
        if (ModifierKeyNames.TryGetValue(vk, out var modifierName)) return modifierName;
        if (MediaKeyNames.TryGetValue(vk, out var mediaName)) return mediaName;

        uint scanCode = User32.MapVirtualKey(vk, User32.MAPVK_VK_TO_VSC);

        int lParam = (int)(scanCode << ScanCodeToLParamShift);

        if (vk >= Vk.PageUp && vk <= Vk.Delete)
            lParam |= ExtendedKeyMask;

        var sb = new StringBuilder(KeyNameBufferSize);
        if (User32.GetKeyNameTextW(lParam, sb, sb.Capacity) > 0)
        {
            return sb.ToString();
        }

        return vk.ToString();
    }

    public int GetKeyCode(string keyName)
    {
        if (keyName.Length == 1)
        {
            var (vk, _, _) = GetVkForChar(keyName[0]);
            if (vk != 0) return WindowsKeyMap.GetEvdevCode((ushort)vk);
        }
        
        
        return 0;
    }

    public char? GetCharFromKeyCode(int keyCode, bool leftShift, bool rightShift, bool rightAlt, bool leftAlt, bool leftCtrl, bool capsLock)
    {
        ushort vk = WindowsKeyMap.GetVirtualKey(keyCode);
        if (vk == 0) return null;

        uint scanCode = User32.MapVirtualKey(vk, User32.MAPVK_VK_TO_VSC);
        
        byte[] keyState = new byte[256];
        
        // Exact modifier mapping
        if (leftShift) 
        {
            keyState[Vk.Shift] = 0x80; // VK_SHIFT
            keyState[Vk.LeftShift] = 0x80; // VK_LSHIFT
        }
        if (rightShift)
        {
            keyState[Vk.Shift] = 0x80; // VK_SHIFT
            keyState[Vk.RightShift] = 0x80; // VK_RSHIFT
        }
        
        if (leftCtrl)
        {
            keyState[Vk.Ctrl] = 0x80; // VK_CONTROL
            keyState[Vk.LeftCtrl] = 0x80; // VK_LCONTROL
        }

        if (leftAlt) 
        {
             keyState[Vk.Alt] = 0x80; // VK_MENU
             keyState[Vk.LeftAlt] = 0x80; // VK_LMENU
        }
        
        // AltGr / Right Alt Logic
        if (rightAlt)
        {
            // For AltGr, Windows expects Ctrl+Alt OR specific Right Alt handling depending on layout
            // Safe bet: Set generic Ctrl+Alt and specific Right Alt/Left Ctrl
            keyState[Vk.Ctrl] = 0x80; // VK_CONTROL
            keyState[Vk.Alt] = 0x80; // VK_MENU
            keyState[Vk.LeftCtrl] = 0x80; // VK_LCONTROL (AltGr usually triggers this)
            keyState[Vk.RightAlt] = 0x80; // VK_RMENU
        }
        
        if (capsLock) 
        {
            keyState[Vk.CapsLock] = 0x01; // VK_CAPITAL
        }

        var sb = new StringBuilder(5);
        
        // Get layout from the foreground window
        IntPtr hwnd = User32.GetForegroundWindow();
        uint threadId = User32.GetWindowThreadProcessId(hwnd, IntPtr.Zero);
        IntPtr layout = User32.GetKeyboardLayout(threadId);
        
        // Fallback to own thread if external lookup failed
        if (layout == IntPtr.Zero)
        {
            layout = User32.GetKeyboardLayout(0);
        }

        int result = User32.ToUnicodeEx(vk, scanCode, keyState, sb, sb.Capacity, 0, layout);
        
        if (result > 0)
        {
            return sb[0];
        }

        return null;
    }

    public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c)
    {
        var result = GetVkForChar(c);
        if (result.vk == 0) return null;
        
        int evdev = WindowsKeyMap.GetEvdevCode((ushort)result.vk);
        if (evdev == 0) return null;

        return (evdev, result.shift, result.altGr);
    }

    private (int vk, bool shift, bool altGr) GetVkForChar(char c)
    {
        IntPtr layout = User32.GetKeyboardLayout(0);
        short scanResult = User32.VkKeyScanEx(c, layout);

        if (scanResult == -1) return (0, false, false);

        int vk = scanResult & 0xFF;
        int shiftState = (scanResult >> 8) & 0xFF;

        bool shift = (shiftState & 1) != 0;
        bool ctrl = (shiftState & 2) != 0;
        bool alt = (shiftState & 4) != 0;
        
        bool altGr = ctrl && alt;
        
        return (vk, shift, altGr);
    }
}
