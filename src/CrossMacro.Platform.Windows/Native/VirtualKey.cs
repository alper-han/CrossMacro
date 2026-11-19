
namespace CrossMacro.Platform.Windows.Native;

/// <summary>Windows virtual-key codes (<c>VK_*</c> in WinUser.h).</summary>
internal static class VirtualKey
{
    public const ushort Back = 0x08;
    public const ushort Tab = 0x09;
    public const ushort Return = 0x0D;
    public const ushort Shift = 0x10;
    public const ushort Control = 0x11;
    public const ushort Menu = 0x12;
    public const ushort Pause = 0x13;
    public const ushort Capital = 0x14;
    public const ushort Hangul = 0x15;
    public const ushort Hanja = 0x19;
    public const ushort Escape = 0x1B;
    public const ushort Space = 0x20;
    public const ushort Prior = 0x21;
    public const ushort Next = 0x22;
    public const ushort End = 0x23;
    public const ushort Home = 0x24;
    public const ushort Left = 0x25;
    public const ushort Up = 0x26;
    public const ushort Right = 0x27;
    public const ushort Down = 0x28;
    public const ushort Snapshot = 0x2C;
    public const ushort Insert = 0x2D;
    public const ushort Delete = 0x2E;
    public const ushort Help = 0x2F;

    public const ushort D0 = 0x30;
    public const ushort D1 = 0x31;
    public const ushort D2 = 0x32;
    public const ushort D3 = 0x33;
    public const ushort D4 = 0x34;
    public const ushort D5 = 0x35;
    public const ushort D6 = 0x36;
    public const ushort D7 = 0x37;
    public const ushort D8 = 0x38;
    public const ushort D9 = 0x39;

    public const ushort A = 0x41;
    public const ushort B = 0x42;
    public const ushort C = 0x43;
    public const ushort D = 0x44;
    public const ushort E = 0x45;
    public const ushort F = 0x46;
    public const ushort G = 0x47;
    public const ushort H = 0x48;
    public const ushort I = 0x49;
    public const ushort J = 0x4A;
    public const ushort K = 0x4B;
    public const ushort L = 0x4C;
    public const ushort M = 0x4D;
    public const ushort N = 0x4E;
    public const ushort O = 0x4F;
    public const ushort P = 0x50;
    public const ushort Q = 0x51;
    public const ushort R = 0x52;
    public const ushort S = 0x53;
    public const ushort T = 0x54;
    public const ushort U = 0x55;
    public const ushort V = 0x56;
    public const ushort W = 0x57;
    public const ushort X = 0x58;
    public const ushort Y = 0x59;
    public const ushort Z = 0x5A;

    public const ushort LeftWindows = 0x5B;
    public const ushort RightWindows = 0x5C;
    public const ushort Apps = 0x5D;

    public const ushort Numpad0 = 0x60;
    public const ushort Numpad1 = 0x61;
    public const ushort Numpad2 = 0x62;
    public const ushort Numpad3 = 0x63;
    public const ushort Numpad4 = 0x64;
    public const ushort Numpad5 = 0x65;
    public const ushort Numpad6 = 0x66;
    public const ushort Numpad7 = 0x67;
    public const ushort Numpad8 = 0x68;
    public const ushort Numpad9 = 0x69;
    public const ushort Multiply = 0x6A;
    public const ushort Add = 0x6B;
    public const ushort Subtract = 0x6D;
    public const ushort Decimal = 0x6E;
    public const ushort Divide = 0x6F;

    public const ushort F1 = 0x70;
    public const ushort F2 = 0x71;
    public const ushort F3 = 0x72;
    public const ushort F4 = 0x73;
    public const ushort F5 = 0x74;
    public const ushort F6 = 0x75;
    public const ushort F7 = 0x76;
    public const ushort F8 = 0x77;
    public const ushort F9 = 0x78;
    public const ushort F10 = 0x79;
    public const ushort F11 = 0x7A;
    public const ushort F12 = 0x7B;
    public const ushort F13 = 0x7C;
    public const ushort F14 = 0x7D;
    public const ushort F15 = 0x7E;
    public const ushort F16 = 0x7F;
    public const ushort F17 = 0x80;
    public const ushort F18 = 0x81;
    public const ushort F19 = 0x82;
    public const ushort F20 = 0x83;
    public const ushort F21 = 0x84;
    public const ushort F22 = 0x85;
    public const ushort F23 = 0x86;
    public const ushort F24 = 0x87;

    public const ushort NumLock = 0x90;
    public const ushort ScrollLock = 0x91;

    public const ushort LeftShift = 0xA0;
    public const ushort RightShift = 0xA1;
    public const ushort LeftControl = 0xA2;
    public const ushort RightControl = 0xA3;
    public const ushort LeftMenu = 0xA4;
    public const ushort RightMenu = 0xA5;

    public const ushort BrowserHome = 0xAC;
    public const ushort VolumeMute = 0xAD;
    public const ushort VolumeDown = 0xAE;
    public const ushort VolumeUp = 0xAF;
    public const ushort MediaNextTrack = 0xB0;
    public const ushort MediaPrevTrack = 0xB1;
    public const ushort MediaStop = 0xB2;
    public const ushort MediaPlayPause = 0xB3;
    public const ushort LaunchMail = 0xB4;
    public const ushort LaunchApp1 = 0xB6;
    public const ushort LaunchApp2 = 0xB7;

    public const ushort Oem1 = 0xBA;
    public const ushort OemPlus = 0xBB;
    public const ushort OemComma = 0xBC;
    public const ushort OemMinus = 0xBD;
    public const ushort OemPeriod = 0xBE;
    public const ushort Oem2 = 0xBF;
    public const ushort Oem3 = 0xC0;
    public const ushort Oem4 = 0xDB;
    public const ushort Oem5 = 0xDC;
    public const ushort Oem6 = 0xDD;
    public const ushort Oem7 = 0xDE;
    public const ushort Oem102 = 0xE2;

    public const ushort Exsel = 0xF8;
    public const ushort Ereof = 0xF9;
    public const ushort Play = 0xFA;
    public const ushort Zoom = 0xFB;
    public const ushort NoName = 0xFC;
    public const ushort Pa1 = 0xFD;
}
