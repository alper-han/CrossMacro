using System.Collections.Frozen;

namespace CrossMacro.Platform.Windows.Helpers;

/// <summary>
/// Maps evdev key codes to Windows virtual keys and back. Each row also records whether the key is an
/// extended key, i.e. one whose scan code carries the 0xE0 prefix and needs KEYEVENTF_EXTENDEDKEY.
/// </summary>
internal static class WindowsKeyMap
{
    private readonly record struct Mapping(int Evdev, ushort Vk, bool IsExtended = false, bool MapsBack = true);

    private static readonly Mapping[] Mappings =
    [
        new(InputEventCode.KEY_A, VirtualKey.A),
        new(InputEventCode.KEY_B, VirtualKey.B),
        new(InputEventCode.KEY_C, VirtualKey.C),
        new(InputEventCode.KEY_D, VirtualKey.D),
        new(InputEventCode.KEY_E, VirtualKey.E),
        new(InputEventCode.KEY_F, VirtualKey.F),
        new(InputEventCode.KEY_G, VirtualKey.G),
        new(InputEventCode.KEY_H, VirtualKey.H),
        new(InputEventCode.KEY_I, VirtualKey.I),
        new(InputEventCode.KEY_J, VirtualKey.J),
        new(InputEventCode.KEY_K, VirtualKey.K),
        new(InputEventCode.KEY_L, VirtualKey.L),
        new(InputEventCode.KEY_M, VirtualKey.M),
        new(InputEventCode.KEY_N, VirtualKey.N),
        new(InputEventCode.KEY_O, VirtualKey.O),
        new(InputEventCode.KEY_P, VirtualKey.P),
        new(InputEventCode.KEY_Q, VirtualKey.Q),
        new(InputEventCode.KEY_R, VirtualKey.R),
        new(InputEventCode.KEY_S, VirtualKey.S),
        new(InputEventCode.KEY_T, VirtualKey.T),
        new(InputEventCode.KEY_U, VirtualKey.U),
        new(InputEventCode.KEY_V, VirtualKey.V),
        new(InputEventCode.KEY_W, VirtualKey.W),
        new(InputEventCode.KEY_X, VirtualKey.X),
        new(InputEventCode.KEY_Y, VirtualKey.Y),
        new(InputEventCode.KEY_Z, VirtualKey.Z),

        new(InputEventCode.KEY_0, VirtualKey.D0),
        new(InputEventCode.KEY_1, VirtualKey.D1),
        new(InputEventCode.KEY_2, VirtualKey.D2),
        new(InputEventCode.KEY_3, VirtualKey.D3),
        new(InputEventCode.KEY_4, VirtualKey.D4),
        new(InputEventCode.KEY_5, VirtualKey.D5),
        new(InputEventCode.KEY_6, VirtualKey.D6),
        new(InputEventCode.KEY_7, VirtualKey.D7),
        new(InputEventCode.KEY_8, VirtualKey.D8),
        new(InputEventCode.KEY_9, VirtualKey.D9),

        new(InputEventCode.KEY_ESC, VirtualKey.Escape),
        new(InputEventCode.KEY_ENTER, VirtualKey.Return),
        new(InputEventCode.KEY_BACKSPACE, VirtualKey.Back),
        new(InputEventCode.KEY_TAB, VirtualKey.Tab),
        new(InputEventCode.KEY_SPACE, VirtualKey.Space),
        new(InputEventCode.KEY_CAPSLOCK, VirtualKey.Capital),

        new(InputEventCode.KEY_LEFTCTRL, VirtualKey.LeftControl),
        new(InputEventCode.KEY_LEFTSHIFT, VirtualKey.LeftShift),
        new(InputEventCode.KEY_LEFTALT, VirtualKey.LeftMenu),
        new(InputEventCode.KEY_LEFTMETA, VirtualKey.LeftWindows, IsExtended: true),
        new(InputEventCode.KEY_RIGHTCTRL, VirtualKey.RightControl, IsExtended: true),
        new(InputEventCode.KEY_RIGHTSHIFT, VirtualKey.RightShift),
        new(InputEventCode.KEY_RIGHTALT, VirtualKey.RightMenu, IsExtended: true),
        new(InputEventCode.KEY_RIGHTMETA, VirtualKey.RightWindows, IsExtended: true),

        new(InputEventCode.KEY_UP, VirtualKey.Up, IsExtended: true),
        new(InputEventCode.KEY_DOWN, VirtualKey.Down, IsExtended: true),
        new(InputEventCode.KEY_LEFT, VirtualKey.Left, IsExtended: true),
        new(InputEventCode.KEY_RIGHT, VirtualKey.Right, IsExtended: true),
        new(InputEventCode.KEY_INSERT, VirtualKey.Insert, IsExtended: true),
        new(InputEventCode.KEY_DELETE, VirtualKey.Delete, IsExtended: true),
        new(InputEventCode.KEY_HOME, VirtualKey.Home, IsExtended: true),
        new(InputEventCode.KEY_END, VirtualKey.End, IsExtended: true),
        new(InputEventCode.KEY_PAGEUP, VirtualKey.Prior, IsExtended: true),
        new(InputEventCode.KEY_PAGEDOWN, VirtualKey.Next, IsExtended: true),

        new(InputEventCode.KEY_F1, VirtualKey.F1),
        new(InputEventCode.KEY_F2, VirtualKey.F2),
        new(InputEventCode.KEY_F3, VirtualKey.F3),
        new(InputEventCode.KEY_F4, VirtualKey.F4),
        new(InputEventCode.KEY_F5, VirtualKey.F5),
        new(InputEventCode.KEY_F6, VirtualKey.F6),
        new(InputEventCode.KEY_F7, VirtualKey.F7),
        new(InputEventCode.KEY_F8, VirtualKey.F8),
        new(InputEventCode.KEY_F9, VirtualKey.F9),
        new(InputEventCode.KEY_F10, VirtualKey.F10),
        new(InputEventCode.KEY_F11, VirtualKey.F11),
        new(InputEventCode.KEY_F12, VirtualKey.F12),
        new(InputEventCode.KEY_F13, VirtualKey.F13),
        new(InputEventCode.KEY_F14, VirtualKey.F14),
        new(InputEventCode.KEY_F15, VirtualKey.F15),
        new(InputEventCode.KEY_F16, VirtualKey.F16),
        new(InputEventCode.KEY_F17, VirtualKey.F17),
        new(InputEventCode.KEY_F18, VirtualKey.F18),
        new(InputEventCode.KEY_F19, VirtualKey.F19),
        new(InputEventCode.KEY_F20, VirtualKey.F20),
        new(InputEventCode.KEY_F21, VirtualKey.F21),
        new(InputEventCode.KEY_F22, VirtualKey.F22),
        new(InputEventCode.KEY_F23, VirtualKey.F23),
        new(InputEventCode.KEY_F24, VirtualKey.F24),

        new(InputEventCode.KEY_KP0, VirtualKey.Numpad0),
        new(InputEventCode.KEY_KP1, VirtualKey.Numpad1),
        new(InputEventCode.KEY_KP2, VirtualKey.Numpad2),
        new(InputEventCode.KEY_KP3, VirtualKey.Numpad3),
        new(InputEventCode.KEY_KP4, VirtualKey.Numpad4),
        new(InputEventCode.KEY_KP5, VirtualKey.Numpad5),
        new(InputEventCode.KEY_KP6, VirtualKey.Numpad6),
        new(InputEventCode.KEY_KP7, VirtualKey.Numpad7),
        new(InputEventCode.KEY_KP8, VirtualKey.Numpad8),
        new(InputEventCode.KEY_KP9, VirtualKey.Numpad9),
        new(InputEventCode.KEY_KPASTERISK, VirtualKey.Multiply),
        new(InputEventCode.KEY_KPPLUS, VirtualKey.Add),
        new(InputEventCode.KEY_KPMINUS, VirtualKey.Subtract),
        new(InputEventCode.KEY_KPDOT, VirtualKey.Decimal),
        new(InputEventCode.KEY_KPSLASH, VirtualKey.Divide, IsExtended: true),

        // The keypad Enter key shares VK_RETURN with the main one and is told apart by the extended flag.
        new(InputEventCode.KEY_KPENTER, VirtualKey.Return, IsExtended: true, MapsBack: false),

        new(InputEventCode.KEY_NUMLOCK, VirtualKey.NumLock, IsExtended: true),
        new(InputEventCode.KEY_SCROLLLOCK, VirtualKey.ScrollLock),
        new(InputEventCode.KEY_SYSRQ, VirtualKey.Snapshot, IsExtended: true),
        new(InputEventCode.KEY_PAUSE, VirtualKey.Pause),

        new(InputEventCode.KEY_MINUS, VirtualKey.OemMinus),
        new(InputEventCode.KEY_EQUAL, VirtualKey.OemPlus),
        new(InputEventCode.KEY_LEFTBRACE, VirtualKey.Oem4),
        new(InputEventCode.KEY_RIGHTBRACE, VirtualKey.Oem6),
        new(InputEventCode.KEY_SEMICOLON, VirtualKey.Oem1),
        new(InputEventCode.KEY_APOSTROPHE, VirtualKey.Oem7),
        new(InputEventCode.KEY_GRAVE, VirtualKey.Oem3),
        new(InputEventCode.KEY_BACKSLASH, VirtualKey.Oem5),
        new(InputEventCode.KEY_COMMA, VirtualKey.OemComma),
        new(InputEventCode.KEY_DOT, VirtualKey.OemPeriod),
        new(InputEventCode.KEY_SLASH, VirtualKey.Oem2),
        new(InputEventCode.KEY_102ND, VirtualKey.Oem102),

        new(InputEventCode.KEY_MUTE, VirtualKey.VolumeMute, IsExtended: true),
        new(InputEventCode.KEY_VOLUMEDOWN, VirtualKey.VolumeDown, IsExtended: true),
        new(InputEventCode.KEY_VOLUMEUP, VirtualKey.VolumeUp, IsExtended: true),
        new(InputEventCode.KEY_NEXTSONG, VirtualKey.MediaNextTrack, IsExtended: true),
        new(InputEventCode.KEY_PREVIOUSSONG, VirtualKey.MediaPrevTrack, IsExtended: true),
        new(InputEventCode.KEY_STOPCD, VirtualKey.MediaStop, IsExtended: true),
        new(InputEventCode.KEY_PLAYPAUSE, VirtualKey.MediaPlayPause, IsExtended: true),
        new(InputEventCode.KEY_MENU, VirtualKey.Apps, IsExtended: true),
        new(InputEventCode.KEY_WWW, VirtualKey.BrowserHome, IsExtended: true),
        new(InputEventCode.KEY_MAIL, VirtualKey.LaunchMail, IsExtended: true),
        new(InputEventCode.KEY_OPEN, VirtualKey.LaunchApp1, IsExtended: true),
        new(InputEventCode.KEY_PROPS, VirtualKey.LaunchApp2, IsExtended: true),

        new(InputEventCode.KEY_HANGUEL, VirtualKey.Hangul),
        new(InputEventCode.KEY_HANJA, VirtualKey.Hanja),

        new(InputEventCode.KEY_STOP, VirtualKey.Exsel),
        new(InputEventCode.KEY_COPY, VirtualKey.Ereof),
        new(InputEventCode.KEY_PASTE, VirtualKey.Play),
        new(InputEventCode.KEY_CUT, VirtualKey.Zoom),
        new(InputEventCode.KEY_UNDO, VirtualKey.NoName),
        new(InputEventCode.KEY_FIND, VirtualKey.Pa1),
        new(InputEventCode.KEY_HELP, VirtualKey.Help),
    ];

    private static readonly FrozenDictionary<int, Mapping> ByEvdev =
        Mappings.ToDictionary(static mapping => mapping.Evdev).ToFrozenDictionary();

    private static readonly FrozenDictionary<ushort, int> EvdevByVirtualKey =
        Mappings.Where(static mapping => mapping.MapsBack)
            .ToDictionary(static mapping => mapping.Vk, static mapping => mapping.Evdev)
            .ToFrozenDictionary();

    public static ushort GetVirtualKey(int evdevCode) =>
        ByEvdev.TryGetValue(evdevCode, out var mapping) ? mapping.Vk : (ushort)0;

    public static int GetEvdevCode(ushort vk) =>
        EvdevByVirtualKey.TryGetValue(vk, out var evdev) ? evdev : 0;

    /// <summary>Whether injecting or describing this key needs the extended-key flag.</summary>
    public static bool NeedsExtendedFlag(int evdevCode) =>
        ByEvdev.TryGetValue(evdevCode, out var mapping) && mapping.IsExtended;
}
