using System.Collections.Generic;
using CrossMacro.Core.Services;

namespace CrossMacro.Platform.Windows.Helpers;

internal static class WindowsKeyMap
{
    private static readonly Dictionary<int, ushort> _evdevToVk = new();
    private static readonly Dictionary<ushort, int> _vkToEvdev = new();

    static WindowsKeyMap()
    {
        Add(InputEventCode.KEY_A, 0x41);
        Add(InputEventCode.KEY_B, 0x42);
        Add(InputEventCode.KEY_C, 0x43);
        Add(InputEventCode.KEY_D, 0x44);
        Add(InputEventCode.KEY_E, 0x45);
        Add(InputEventCode.KEY_F, 0x46);
        Add(InputEventCode.KEY_G, 0x47);
        Add(InputEventCode.KEY_H, 0x48);
        Add(InputEventCode.KEY_I, 0x49);
        Add(InputEventCode.KEY_J, 0x4A);
        Add(InputEventCode.KEY_K, 0x4B);
        Add(InputEventCode.KEY_L, 0x4C);
        Add(InputEventCode.KEY_M, 0x4D);
        Add(InputEventCode.KEY_N, 0x4E);
        Add(InputEventCode.KEY_O, 0x4F);
        Add(InputEventCode.KEY_P, 0x50);
        Add(InputEventCode.KEY_Q, 0x51);
        Add(InputEventCode.KEY_R, 0x52);
        Add(InputEventCode.KEY_S, 0x53);
        Add(InputEventCode.KEY_T, 0x54);
        Add(InputEventCode.KEY_U, 0x55);
        Add(InputEventCode.KEY_V, 0x56);
        Add(InputEventCode.KEY_W, 0x57);
        Add(InputEventCode.KEY_X, 0x58);
        Add(InputEventCode.KEY_Y, 0x59);
        Add(InputEventCode.KEY_Z, 0x5A);
        
        Add(InputEventCode.KEY_0, 0x30);
        Add(InputEventCode.KEY_1, 0x31);
        Add(InputEventCode.KEY_2, 0x32);
        Add(InputEventCode.KEY_3, 0x33);
        Add(InputEventCode.KEY_4, 0x34);
        Add(InputEventCode.KEY_5, 0x35);
        Add(InputEventCode.KEY_6, 0x36);
        Add(InputEventCode.KEY_7, 0x37);
        Add(InputEventCode.KEY_8, 0x38);
        Add(InputEventCode.KEY_9, 0x39);
        
        Add(InputEventCode.KEY_ESC, 0x1B);      
        Add(InputEventCode.KEY_ENTER, 0x0D);    
        Add(InputEventCode.KEY_LEFTCTRL, 0xA2);
        Add(InputEventCode.KEY_LEFTSHIFT, 0xA0);
        Add(InputEventCode.KEY_LEFTALT, 0xA4);  
        Add(InputEventCode.KEY_LEFTMETA, 0x5B);
        Add(InputEventCode.KEY_RIGHTCTRL, 0xA3);
        Add(InputEventCode.KEY_RIGHTSHIFT, 0xA1);
        Add(InputEventCode.KEY_RIGHTALT, 0xA5); 
        Add(InputEventCode.KEY_RIGHTMETA, 0x5C);
        Add(InputEventCode.KEY_BACKSPACE, 0x08);
        Add(InputEventCode.KEY_TAB, 0x09);      
        Add(InputEventCode.KEY_SPACE, 0x20);    
        Add(InputEventCode.KEY_CAPSLOCK, 0x14); 
        
        Add(InputEventCode.KEY_UP, 0x26);       
        Add(InputEventCode.KEY_DOWN, 0x28);     
        Add(InputEventCode.KEY_LEFT, 0x25);     
        Add(InputEventCode.KEY_RIGHT, 0x27);    
        Add(InputEventCode.KEY_INSERT, 0x2D);   
        Add(InputEventCode.KEY_DELETE, 0x2E);   
        Add(InputEventCode.KEY_HOME, 0x24);     
        Add(InputEventCode.KEY_END, 0x23);      
        Add(InputEventCode.KEY_PAGEUP, 0x21);   
        Add(InputEventCode.KEY_PAGEDOWN, 0x22); 
        
        Add(InputEventCode.KEY_F1, 0x70);
        Add(InputEventCode.KEY_F2, 0x71);
        Add(InputEventCode.KEY_F3, 0x72);
        Add(InputEventCode.KEY_F4, 0x73);
        Add(InputEventCode.KEY_F5, 0x74);
        Add(InputEventCode.KEY_F6, 0x75);
        Add(InputEventCode.KEY_F7, 0x76);
        Add(InputEventCode.KEY_F8, 0x77);
        Add(InputEventCode.KEY_F9, 0x78);
        Add(InputEventCode.KEY_F10, 0x79);
        Add(InputEventCode.KEY_F11, 0x7A);
        Add(InputEventCode.KEY_F12, 0x7B);
        
        Add(InputEventCode.KEY_MINUS, 0xBD);    
        Add(InputEventCode.KEY_EQUAL, 0xBB);    
        Add(InputEventCode.KEY_LEFTBRACE, 0xDB);
        Add(InputEventCode.KEY_RIGHTBRACE, 0xDD);
        Add(InputEventCode.KEY_SEMICOLON, 0xBA);
        Add(InputEventCode.KEY_APOSTROPHE, 0xDE);
        Add(InputEventCode.KEY_GRAVE, 0xC0);    
        Add(InputEventCode.KEY_BACKSLASH, 0xDC);
        Add(InputEventCode.KEY_COMMA, 0xBC);    
        Add(InputEventCode.KEY_DOT, 0xBE);      
        Add(InputEventCode.KEY_SLASH, 0xBF);    
        
        Add(71, 0x67);  
        Add(72, 0x68);  
        Add(73, 0x69);  
        Add(74, 0x6D);  
        Add(75, 0x64);  
        Add(76, 0x65);  
        Add(77, 0x66);  
        Add(78, 0x6B);  
        Add(79, 0x61);  
        Add(80, 0x62);  
        Add(81, 0x63);  
        Add(82, 0x60);  
        Add(83, 0x6E);  
        Add(96, 0x0D);  
        Add(98, 0x6F);  
        Add(55, 0x6A);  
        Add(69, 0x90);  
        Add(70, 0x91);  
        Add(99, 0x2C);  
        Add(119, 0x13);
    }

    private static void Add(int evdev, ushort vk)
    {
        if (!_evdevToVk.ContainsKey(evdev)) _evdevToVk[evdev] = vk;
        if (!_vkToEvdev.ContainsKey(vk)) _vkToEvdev[vk] = evdev;
    }

    public static ushort GetVirtualKey(int evdevCode)
    {
        return _evdevToVk.TryGetValue(evdevCode, out var vk) ? vk : (ushort)0;
    }
    
    public static int GetEvdevCode(ushort vk)
    {
        return _vkToEvdev.TryGetValue(vk, out var evdev) ? evdev : 0;
    }
}
