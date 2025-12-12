using System.Runtime.InteropServices;
using System.Text;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Windows.Helpers;
using CrossMacro.Platform.Windows.Native;

namespace CrossMacro.Platform.Windows;

public class WindowsKeyboardLayoutService : IKeyboardLayoutService
{
    public string GetKeyName(int keyCode)
    {
        ushort vk = WindowsKeyMap.GetVirtualKey(keyCode);
        if (vk == 0) return $"Key_{keyCode}";
        
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

    public char? GetCharFromKeyCode(int keyCode, bool shift, bool altGr, bool capsLock)
    {
        ushort vk = WindowsKeyMap.GetVirtualKey(keyCode);
        if (vk == 0) return null;

        uint scanCode = User32.MapVirtualKey(vk, User32.MAPVK_VK_TO_VSC);
        
        byte[] keyState = new byte[256];
        if (shift) keyState[0x10] = 0x80; 
        if (altGr) 
        {
            keyState[0x11] = 0x80; 
            keyState[0x12] = 0x80; 
        }
        if (capsLock) keyState[0x14] = 0x01;

        var sb = new StringBuilder(5);
        IntPtr layout = User32.GetKeyboardLayout(0);
        
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
