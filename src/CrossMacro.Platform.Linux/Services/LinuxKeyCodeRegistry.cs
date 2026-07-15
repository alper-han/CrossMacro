
namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// Registry for Linux evdev key codes with embedded lookup table.
/// Based on Linux kernel input-event-codes.h
/// </summary>
public static class LinuxKeyCodeRegistry
{
    /// <summary>
    /// Maximum key code value (KEY_MAX from input-event-codes.h)
    /// </summary>
    public const int KEY_MAX = 0x2FF;

    /// <summary>
    /// Static lookup table: key code → key name
    /// </summary>
    private static readonly Dictionary<int, string> KeyNames = new()
    {
        // Reserved
        { 0, "KEY_RESERVED" },

        // Function row
        { 1, "KEY_ESC" },
        { 2, "KEY_1" },
        { 3, "KEY_2" },
        { 4, "KEY_3" },
        { 5, "KEY_4" },
        { 6, "KEY_5" },
        { 7, "KEY_6" },
        { 8, "KEY_7" },
        { 9, "KEY_8" },
        { 10, "KEY_9" },
        { 11, "KEY_0" },
        { 12, "KEY_MINUS" },
        { 13, "KEY_EQUAL" },
        { 14, "KEY_BACKSPACE" },
        { 15, "KEY_TAB" },

        // QWERTY row 1
        { 16, "KEY_Q" },
        { 17, "KEY_W" },
        { 18, "KEY_E" },
        { 19, "KEY_R" },
        { 20, "KEY_T" },
        { 21, "KEY_Y" },
        { 22, "KEY_U" },
        { 23, "KEY_I" },
        { 24, "KEY_O" },
        { 25, "KEY_P" },
        { 26, "KEY_LEFTBRACE" },
        { 27, "KEY_RIGHTBRACE" },
        { 28, "KEY_ENTER" },
        { 29, "KEY_LEFTCTRL" },

        // QWERTY row 2
        { 30, "KEY_A" },
        { 31, "KEY_S" },
        { 32, "KEY_D" },
        { 33, "KEY_F" },
        { 34, "KEY_G" },
        { 35, "KEY_H" },
        { 36, "KEY_J" },
        { 37, "KEY_K" },
        { 38, "KEY_L" },
        { 39, "KEY_SEMICOLON" },
        { 40, "KEY_APOSTROPHE" },
        { 41, "KEY_GRAVE" },
        { 42, "KEY_LEFTSHIFT" },
        { 43, "KEY_BACKSLASH" },

        // QWERTY row 3
        { 44, "KEY_Z" },
        { 45, "KEY_X" },
        { 46, "KEY_C" },
        { 47, "KEY_V" },
        { 48, "KEY_B" },
        { 49, "KEY_N" },
        { 50, "KEY_M" },
        { 51, "KEY_COMMA" },
        { 52, "KEY_DOT" },
        { 53, "KEY_SLASH" },
        { 54, "KEY_RIGHTSHIFT" },
        { 55, "KEY_KPASTERISK" },
        { 56, "KEY_LEFTALT" },
        { 57, "KEY_SPACE" },
        { 58, "KEY_CAPSLOCK" },

        // Function keys F1-F10
        { 59, "KEY_F1" },
        { 60, "KEY_F2" },
        { 61, "KEY_F3" },
        { 62, "KEY_F4" },
        { 63, "KEY_F5" },
        { 64, "KEY_F6" },
        { 65, "KEY_F7" },
        { 66, "KEY_F8" },
        { 67, "KEY_F9" },
        { 68, "KEY_F10" },

        // Lock keys
        { 69, "KEY_NUMLOCK" },
        { 70, "KEY_SCROLLLOCK" },

        // Numpad
        { 71, "KEY_KP7" },
        { 72, "KEY_KP8" },
        { 73, "KEY_KP9" },
        { 74, "KEY_KPMINUS" },
        { 75, "KEY_KP4" },
        { 76, "KEY_KP5" },
        { 77, "KEY_KP6" },
        { 78, "KEY_KPPLUS" },
        { 79, "KEY_KP1" },
        { 80, "KEY_KP2" },
        { 81, "KEY_KP3" },
        { 82, "KEY_KP0" },
        { 83, "KEY_KPDOT" },

        // International keys
        { 85, "KEY_ZENKAKUHANKAKU" },
        { 86, "KEY_102ND" },
        { 87, "KEY_F11" },
        { 88, "KEY_F12" },
        { 89, "KEY_RO" },
        { 90, "KEY_KATAKANA" },
        { 91, "KEY_HIRAGANA" },
        { 92, "KEY_HENKAN" },
        { 93, "KEY_KATAKANAHIRAGANA" },
        { 94, "KEY_MUHENKAN" },
        { 95, "KEY_KPJPCOMMA" },
        { 96, "KEY_KPENTER" },
        { 97, "KEY_RIGHTCTRL" },
        { 98, "KEY_KPSLASH" },
        { 99, "KEY_SYSRQ" },
        { 100, "KEY_RIGHTALT" },
        { 101, "KEY_LINEFEED" },
        { 102, "KEY_HOME" },
        { 103, "KEY_UP" },
        { 104, "KEY_PAGEUP" },
        { 105, "KEY_LEFT" },
        { 106, "KEY_RIGHT" },
        { 107, "KEY_END" },
        { 108, "KEY_DOWN" },
        { 109, "KEY_PAGEDOWN" },
        { 110, "KEY_INSERT" },
        { 111, "KEY_DELETE" },
        { 112, "KEY_MACRO" },
        { 113, "KEY_MUTE" },
        { 114, "KEY_VOLUMEDOWN" },
        { 115, "KEY_VOLUMEUP" },
        { 116, "KEY_POWER" },
        { 117, "KEY_KPEQUAL" },
        { 118, "KEY_KPPLUSMINUS" },
        { 119, "KEY_PAUSE" },
        { 120, "KEY_SCALE" },
        { 121, "KEY_KPCOMMA" },
        { 122, "KEY_HANGUEL" },
        { 123, "KEY_HANJA" },
        { 124, "KEY_YEN" },
        { 125, "KEY_LEFTMETA" },
        { 126, "KEY_RIGHTMETA" },
        { 127, "KEY_COMPOSE" },

        // System control
        { 128, "KEY_STOP" },
        { 129, "KEY_AGAIN" },
        { 130, "KEY_PROPS" },
        { 131, "KEY_UNDO" },
        { 132, "KEY_FRONT" },
        { 133, "KEY_COPY" },
        { 134, "KEY_OPEN" },
        { 135, "KEY_PASTE" },
        { 136, "KEY_FIND" },
        { 137, "KEY_CUT" },
        { 138, "KEY_HELP" },
        { 139, "KEY_MENU" },
        { 140, "KEY_CALC" },
        { 141, "KEY_SETUP" },
        { 142, "KEY_SLEEP" },
        { 143, "KEY_WAKEUP" },
        { 144, "KEY_FILE" },
        { 145, "KEY_SENDFILE" },
        { 146, "KEY_DELETEFILE" },
        { 147, "KEY_XFER" },
        { 148, "KEY_PROG1" },
        { 149, "KEY_PROG2" },
        { 150, "KEY_WWW" },
        { 151, "KEY_MSDOS" },
        { 152, "KEY_COFFEE" },
        { 153, "KEY_ROTATE_DISPLAY" },
        { 154, "KEY_CYCLEWINDOWS" },
        { 155, "KEY_MAIL" },
        { 156, "KEY_BOOKMARKS" },
        { 157, "KEY_COMPUTER" },
        { 158, "KEY_BACK" },
        { 159, "KEY_FORWARD" },
        { 160, "KEY_CLOSECD" },
        { 161, "KEY_EJECTCD" },
        { 162, "KEY_EJECTCLOSECD" },
        { 163, "KEY_NEXTSONG" },
        { 164, "KEY_PLAYPAUSE" },
        { 165, "KEY_PREVIOUSSONG" },
        { 166, "KEY_STOPCD" },
        { 167, "KEY_RECORD" },
        { 168, "KEY_REWIND" },
        { 169, "KEY_PHONE" },
        { 170, "KEY_ISO" },
        { 171, "KEY_CONFIG" },
        { 172, "KEY_HOMEPAGE" },
        { 173, "KEY_REFRESH" },
        { 174, "KEY_EXIT" },
        { 175, "KEY_MOVE" },
        { 176, "KEY_EDIT" },
        { 177, "KEY_SCROLLUP" },
        { 178, "KEY_SCROLLDOWN" },
        { 179, "KEY_KPLEFTPAREN" },
        { 180, "KEY_KPRIGHTPAREN" },
        { 181, "KEY_NEW" },
        { 182, "KEY_REDO" },

        // Extended Function Keys
        { 183, "KEY_F13" },
        { 184, "KEY_F14" },
        { 185, "KEY_F15" },
        { 186, "KEY_F16" },
        { 187, "KEY_F17" },
        { 188, "KEY_F18" },
        { 189, "KEY_F19" },
        { 190, "KEY_F20" },
        { 191, "KEY_F21" },
        { 192, "KEY_F22" },
        { 193, "KEY_F23" },
        { 194, "KEY_F24" },

        // More system keys
        { 200, "KEY_PLAYCD" },
        { 201, "KEY_PAUSECD" },
        { 202, "KEY_PROG3" },
        { 203, "KEY_PROG4" },
        { 204, "KEY_DASHBOARD" },
        { 205, "KEY_SUSPEND" },
        { 206, "KEY_CLOSE" },
        { 207, "KEY_PLAY" },
        { 208, "KEY_FASTFORWARD" },
        { 209, "KEY_BASSBOOST" },
        { 210, "KEY_PRINT" },
        { 211, "KEY_HP" },
        { 212, "KEY_CAMERA" },
        { 213, "KEY_SOUND" },
        { 214, "KEY_QUESTION" },
        { 215, "KEY_EMAIL" },
        { 216, "KEY_CHAT" },
        { 217, "KEY_SEARCH" },
        { 218, "KEY_CONNECT" },
        { 219, "KEY_FINANCE" },
        { 220, "KEY_SPORT" },
        { 221, "KEY_SHOP" },
        { 222, "KEY_ALTERASE" },
        { 223, "KEY_CANCEL" },
        { 224, "KEY_BRIGHTNESSDOWN" },
        { 225, "KEY_BRIGHTNESSUP" },
        { 226, "KEY_MEDIA" },
        { 227, "KEY_SWITCHVIDEOMODE" },
        { 228, "KEY_KBDILLUMTOGGLE" },
        { 229, "KEY_KBDILLUMDOWN" },
        { 230, "KEY_KBDILLUMUP" },
        { 231, "KEY_SEND" },
        { 232, "KEY_REPLY" },
        { 233, "KEY_FORWARDMAIL" },
        { 234, "KEY_SAVE" },
        { 235, "KEY_DOCUMENTS" },
        { 236, "KEY_BATTERY" },
        { 237, "KEY_BLUETOOTH" },
        { 238, "KEY_WLAN" },
        { 239, "KEY_UWB" },
        { 240, "KEY_UNKNOWN" },
        { 241, "KEY_VIDEO_NEXT" },
        { 242, "KEY_VIDEO_PREV" },
        { 243, "KEY_BRIGHTNESS_CYCLE" },
        { 244, "KEY_BRIGHTNESS_AUTO" },
        { 245, "KEY_DISPLAY_OFF" },
        { 246, "KEY_WWAN" },
        { 247, "KEY_RFKILL" },
        { 248, "KEY_MICMUTE" },

        // Mouse buttons (BTN_MISC to BTN_MOUSE range: 0x100-0x11F)
        { 0x110, "BTN_LEFT" },
        { 0x111, "BTN_RIGHT" },
        { 0x112, "BTN_MIDDLE" },
        { 0x113, "BTN_SIDE" },
        { 0x114, "BTN_EXTRA" },
        { 0x115, "BTN_FORWARD" },
        { 0x116, "BTN_BACK" },
        { 0x117, "BTN_TASK" },

        // Gamepad buttons (BTN_GAMEPAD range: 0x130-0x13F)
        { 0x130, "BTN_SOUTH" },
        { 0x131, "BTN_EAST" },
        { 0x132, "BTN_C" },
        { 0x133, "BTN_NORTH" },
        { 0x134, "BTN_WEST" },
        { 0x135, "BTN_Z" },
        { 0x136, "BTN_TL" },
        { 0x137, "BTN_TR" },
        { 0x138, "BTN_TL2" },
        { 0x139, "BTN_TR2" },
        { 0x13A, "BTN_SELECT" },
        { 0x13B, "BTN_START" },
        { 0x13C, "BTN_MODE" },
        { 0x13D, "BTN_THUMBL" },
        { 0x13E, "BTN_THUMBR" },

        // Touch button
        { 0x14A, "BTN_TOUCH" },
        { 0x14B, "BTN_STYLUS" },
        { 0x14C, "BTN_STYLUS2" },
        { 0x14D, "BTN_TOOL_DOUBLETAP" },
        { 0x14E, "BTN_TOOL_TRIPLETAP" },
        { 0x14F, "BTN_TOOL_QUADTAP" },
    };

    /// <summary>
    /// Gets the name for a key code, or generates one if unknown
    /// </summary>
    public static string GetKeyName(int keyCode)
    {
        if (KeyNames.TryGetValue(keyCode, out var name))
            return name;

        // Generate name for unknown codes
        if (keyCode >= 0x100 && keyCode < 0x120)
            return $"BTN_{keyCode:X3}";

        return $"KEY_{keyCode}";
    }

    /// <summary>
    /// Gets all known key names
    /// </summary>
    public static IReadOnlyDictionary<int, string> GetAllKeyNames() => KeyNames;

    /// <summary>
    /// Gets a display-friendly name for a key code
    /// </summary>
    public static string GetDisplayName(int keyCode)
    {
        var name = GetKeyName(keyCode);

        // Remove KEY_ or BTN_ prefix for display
        if (name.StartsWith("KEY_", StringComparison.Ordinal))
            return name[4..];
        if (name.StartsWith("BTN_", StringComparison.Ordinal))
            return name[4..];

        return name;
    }
}
