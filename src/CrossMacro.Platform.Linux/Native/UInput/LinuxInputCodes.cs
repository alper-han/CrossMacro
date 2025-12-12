using System;

namespace CrossMacro.Platform.Linux.Native.UInput;

public static class LinuxInputCodes
{
    public const ushort KEY_RESERVED = 0;
    public const ushort KEY_ESC = 1;
    
    public const ushort KEY_1 = 2;
    public const ushort KEY_2 = 3;
    public const ushort KEY_3 = 4;
    public const ushort KEY_4 = 5;
    public const ushort KEY_5 = 6;
    public const ushort KEY_6 = 7;
    public const ushort KEY_7 = 8;
    public const ushort KEY_8 = 9;
    public const ushort KEY_9 = 10;
    public const ushort KEY_0 = 11;
    public const ushort KEY_MINUS = 12;
    public const ushort KEY_EQUAL = 13;
    public const ushort KEY_BACKSPACE = 14;
    public const ushort KEY_TAB = 15;
    
    public const ushort KEY_Q = 16;
    public const ushort KEY_W = 17;
    public const ushort KEY_E = 18;
    public const ushort KEY_R = 19;
    public const ushort KEY_T = 20;
    public const ushort KEY_Y = 21;
    public const ushort KEY_U = 22;
    public const ushort KEY_I = 23;
    public const ushort KEY_O = 24;
    public const ushort KEY_P = 25;
    public const ushort KEY_LEFTBRACE = 26;
    public const ushort KEY_RIGHTBRACE = 27;
    public const ushort KEY_ENTER = 28;
    public const ushort KEY_LEFTCTRL = 29;
    
    public const ushort KEY_A = 30;
    public const ushort KEY_S = 31;
    public const ushort KEY_D = 32;
    public const ushort KEY_F = 33;
    public const ushort KEY_G = 34;
    public const ushort KEY_H = 35;
    public const ushort KEY_J = 36;
    public const ushort KEY_K = 37;
    public const ushort KEY_L = 38;
    public const ushort KEY_SEMICOLON = 39;
    public const ushort KEY_APOSTROPHE = 40;
    public const ushort KEY_GRAVE = 41;
    public const ushort KEY_LEFTSHIFT = 42;
    public const ushort KEY_BACKSLASH = 43;
    
    public const ushort KEY_Z = 44;
    public const ushort KEY_X = 45;
    public const ushort KEY_C = 46;
    public const ushort KEY_V = 47;
    public const ushort KEY_B = 48;
    public const ushort KEY_N = 49;
    public const ushort KEY_M = 50;
    public const ushort KEY_COMMA = 51;
    public const ushort KEY_DOT = 52;
    public const ushort KEY_SLASH = 53;
    public const ushort KEY_RIGHTSHIFT = 54;
    public const ushort KEY_KPASTERISK = 55;
    public const ushort KEY_LEFTALT = 56;
    public const ushort KEY_SPACE = 57;
    public const ushort KEY_CAPSLOCK = 58;
    
    public const ushort KEY_F1 = 59;
    public const ushort KEY_F2 = 60;
    public const ushort KEY_F3 = 61;
    public const ushort KEY_F4 = 62;
    public const ushort KEY_F5 = 63;
    public const ushort KEY_F6 = 64;
    public const ushort KEY_F7 = 65;
    public const ushort KEY_F8 = 66;
    public const ushort KEY_F9 = 67;
    public const ushort KEY_F10 = 68;
    
    public const ushort KEY_NUMLOCK = 69;
    public const ushort KEY_SCROLLLOCK = 70;
    public const ushort KEY_KP7 = 71;
    public const ushort KEY_KP8 = 72;
    public const ushort KEY_KP9 = 73;
    public const ushort KEY_KPMINUS = 74;
    public const ushort KEY_KP4 = 75;
    public const ushort KEY_KP5 = 76;
    public const ushort KEY_KP6 = 77;
    public const ushort KEY_KPPLUS = 78;
    public const ushort KEY_KP1 = 79;
    public const ushort KEY_KP2 = 80;
    public const ushort KEY_KP3 = 81;
    public const ushort KEY_KP0 = 82;
    public const ushort KEY_KPDOT = 83;
    
    public const ushort KEY_F11 = 87;
    public const ushort KEY_F12 = 88;
    
    public const ushort KEY_KPENTER = 96;
    public const ushort KEY_RIGHTCTRL = 97;
    public const ushort KEY_KPSLASH = 98;
    public const ushort KEY_SYSRQ = 99;
    public const ushort KEY_RIGHTALT = 100;
    
    public const ushort KEY_HOME = 102;
    public const ushort KEY_UP = 103;
    public const ushort KEY_PAGEUP = 104;
    public const ushort KEY_LEFT = 105;
    public const ushort KEY_RIGHT = 106;
    public const ushort KEY_END = 107;
    public const ushort KEY_DOWN = 108;
    public const ushort KEY_PAGEDOWN = 109;
    public const ushort KEY_INSERT = 110;
    public const ushort KEY_DELETE = 111;
    
    public const ushort KEY_MUTE = 113;
    public const ushort KEY_VOLUMEDOWN = 114;
    public const ushort KEY_VOLUMEUP = 115;
    public const ushort KEY_POWER = 116;
    public const ushort KEY_KPEQUAL = 117;
    public const ushort KEY_PAUSE = 119;
    
    public const ushort KEY_LEFTMETA = 125; 
    public const ushort KEY_RIGHTMETA = 126;
    public const ushort KEY_COMPOSE = 127;
    
    public const ushort KEY_F13 = 183;
    public const ushort KEY_F14 = 184;
    public const ushort KEY_F15 = 185;
    public const ushort KEY_F16 = 186;
    public const ushort KEY_F17 = 187;
    public const ushort KEY_F18 = 188;
    public const ushort KEY_F19 = 189;
    public const ushort KEY_F20 = 190;
    public const ushort KEY_F21 = 191;
    public const ushort KEY_F22 = 192;
    public const ushort KEY_F23 = 193;
    public const ushort KEY_F24 = 194;
    
    public static bool IsValidKeyCode(int keyCode)
    {
        return keyCode > 0 && keyCode <= 255;
    }
    
    public static string GetKeyName(int keyCode)
    {
        return keyCode switch
        {
            KEY_ESC => "ESC",
            KEY_1 => "1", KEY_2 => "2", KEY_3 => "3", KEY_4 => "4", KEY_5 => "5",
            KEY_6 => "6", KEY_7 => "7", KEY_8 => "8", KEY_9 => "9", KEY_0 => "0",
            KEY_MINUS => "-", KEY_EQUAL => "=", KEY_BACKSPACE => "BACKSPACE",
            KEY_TAB => "TAB",
            KEY_Q => "Q", KEY_W => "W", KEY_E => "E", KEY_R => "R", KEY_T => "T",
            KEY_Y => "Y", KEY_U => "U", KEY_I => "I", KEY_O => "O", KEY_P => "P",
            KEY_LEFTBRACE => "[", KEY_RIGHTBRACE => "]", KEY_ENTER => "ENTER",
            KEY_LEFTCTRL => "LEFT_CTRL",
            KEY_A => "A", KEY_S => "S", KEY_D => "D", KEY_F => "F", KEY_G => "G",
            KEY_H => "H", KEY_J => "J", KEY_K => "K", KEY_L => "L",
            KEY_SEMICOLON => ";", KEY_APOSTROPHE => "'", KEY_GRAVE => "`",
            KEY_LEFTSHIFT => "LEFT_SHIFT", KEY_BACKSLASH => "\\",
            KEY_Z => "Z", KEY_X => "X", KEY_C => "C", KEY_V => "V", KEY_B => "B",
            KEY_N => "N", KEY_M => "M", KEY_COMMA => ",", KEY_DOT => ".",
            KEY_SLASH => "/", KEY_RIGHTSHIFT => "RIGHT_SHIFT",
            KEY_LEFTALT => "LEFT_ALT", KEY_SPACE => "SPACE", KEY_CAPSLOCK => "CAPSLOCK",
            KEY_F1 => "F1", KEY_F2 => "F2", KEY_F3 => "F3", KEY_F4 => "F4",
            KEY_F5 => "F5", KEY_F6 => "F6", KEY_F7 => "F7", KEY_F8 => "F8",
            KEY_F9 => "F9", KEY_F10 => "F10", KEY_F11 => "F11", KEY_F12 => "F12",
            KEY_NUMLOCK => "NUMLOCK", KEY_SCROLLLOCK => "SCROLLLOCK",
            KEY_HOME => "HOME", KEY_UP => "UP", KEY_PAGEUP => "PAGEUP",
            KEY_LEFT => "LEFT", KEY_RIGHT => "RIGHT", KEY_END => "END",
            KEY_DOWN => "DOWN", KEY_PAGEDOWN => "PAGEDOWN",
            KEY_INSERT => "INSERT", KEY_DELETE => "DELETE",
            KEY_MUTE => "MUTE", KEY_VOLUMEDOWN => "VOL_DOWN", KEY_VOLUMEUP => "VOL_UP",
            KEY_LEFTMETA => "LEFT_SUPER", KEY_RIGHTMETA => "RIGHT_SUPER",
            KEY_RIGHTCTRL => "RIGHT_CTRL", KEY_RIGHTALT => "RIGHT_ALT",
            _ => $"KEY_{keyCode}"
        };
    }
}
