using System.Runtime.InteropServices;
using System.Text;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Windows.Helpers;
using CrossMacro.Platform.Windows.Native;

namespace CrossMacro.Platform.Windows.Services;

public class WindowsKeyboardLayoutService : IKeyboardLayoutService
{
    // TODO: Keyboard control is not yet fully accurate. Needs dedicated time for comprehensive testing to ensure correct behavior across all keyboard layouts.
    public string GetKeyName(int keyCode)
    {
        ushort vk = WindowsKeyMap.GetVirtualKey(keyCode);
        if (vk == 0) return $"Key_{keyCode}";
        if (vk == 0x13) return "Pause";
        if (vk == 0x2C) return "PrintScreen";
        if (vk == 0x90) return "NumLock";
        if (vk == 0x91) return "ScrollLock";
        if (vk == 0x14) return "CapsLock";
        
        // Modifiers
        if (vk == 0xA0 || vk == 0x10) return "LeftShift";
        if (vk == 0xA1) return "RightShift";
        if (vk == 0xA2 || vk == 0x11) return "LeftCtrl";
        if (vk == 0xA3) return "RightCtrl";
        if (vk == 0xA4 || vk == 0x12) return "LeftAlt";
        if (vk == 0xA5) return "RightAlt";
        if (vk == 0x5B) return "LeftWin";
        if (vk == 0x5C) return "RightWin";
        if (vk == 0x5D) return "Menu";

        // Media
        if (vk == 0xAD) return "VolumeMute";
        if (vk == 0xAE) return "VolumeDown";
        if (vk == 0xAF) return "VolumeUp";
        if (vk == 0xB3) return "PlayPause";
        if (vk == 0xB0) return "MediaNext";
        if (vk == 0xB1) return "MediaPrev";
        if (vk == 0xB2) return "MediaStop";
        
        uint scanCode = User32.MapVirtualKey(vk, User32.MAPVK_VK_TO_VSC);
        
        int lParam = (int)(scanCode << 16);
        
        if (vk >= 0x21 && vk <= 0x2E) 
             lParam |= (1 << 24);

        var sb = new StringBuilder(256);
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
            keyState[0x10] = 0x80; // VK_SHIFT
            keyState[0xA0] = 0x80; // VK_LSHIFT
        }
        if (rightShift)
        {
            keyState[0x10] = 0x80; // VK_SHIFT
            keyState[0xA1] = 0x80; // VK_RSHIFT
        }
        
        if (leftCtrl)
        {
            keyState[0x11] = 0x80; // VK_CONTROL
            keyState[0xA2] = 0x80; // VK_LCONTROL
        }

        if (leftAlt) 
        {
             keyState[0x12] = 0x80; // VK_MENU
             keyState[0xA4] = 0x80; // VK_LMENU
        }
        
        // AltGr / Right Alt Logic
        if (rightAlt)
        {
            // For AltGr, Windows expects Ctrl+Alt OR specific Right Alt handling depending on layout
            // Safe bet: Set generic Ctrl+Alt and specific Right Alt/Left Ctrl
            keyState[0x11] = 0x80; // VK_CONTROL
            keyState[0x12] = 0x80; // VK_MENU
            keyState[0xA2] = 0x80; // VK_LCONTROL (AltGr usually triggers this)
            keyState[0xA5] = 0x80; // VK_RMENU
        }
        
        if (capsLock) 
        {
            keyState[0x14] = 0x01; // VK_CAPITAL
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
        bool hankaku = (shiftState & 8) != 0; 
        
        bool altGr = ctrl && alt;
        
        return (vk, shift, altGr);
    }
}
