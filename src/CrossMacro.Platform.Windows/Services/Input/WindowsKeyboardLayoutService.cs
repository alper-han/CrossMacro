
using System.Collections.Frozen;

namespace CrossMacro.Platform.Windows.Services.Input;

internal sealed class WindowsKeyboardLayoutService : IKeyboardLayoutService
{
    private const byte KeyDownState = 0x80;
    private const byte KeyToggledState = 0x01;
    private const int ScanCodeToLParamShift = 16;
    private const int ExtendedKeyMask = 1 << 24;
    private const int KeyNameBufferSize = 256;
    private const int UnicodeBufferSize = 5;
    private const int KeyStateSize = 256;

    // Keys whose Windows key-name text is localized or empty: they keep a stable English name.
    private static readonly FrozenDictionary<ushort, string> FixedKeyNames = new Dictionary<ushort, string>
    {
        [VirtualKey.Pause] = "Pause",
        [VirtualKey.Snapshot] = "PrintScreen",
        [VirtualKey.NumLock] = "NumLock",
        [VirtualKey.ScrollLock] = "ScrollLock",
        [VirtualKey.Capital] = "CapsLock",

        [VirtualKey.LeftShift] = "LeftShift",
        [VirtualKey.Shift] = "LeftShift",
        [VirtualKey.RightShift] = "RightShift",
        [VirtualKey.LeftControl] = "LeftCtrl",
        [VirtualKey.Control] = "LeftCtrl",
        [VirtualKey.RightControl] = "RightCtrl",
        [VirtualKey.LeftMenu] = "LeftAlt",
        [VirtualKey.Menu] = "LeftAlt",
        [VirtualKey.RightMenu] = "RightAlt",
        [VirtualKey.LeftWindows] = "LeftWin",
        [VirtualKey.RightWindows] = "RightWin",
        [VirtualKey.Apps] = "Menu",

        [VirtualKey.VolumeMute] = "VolumeMute",
        [VirtualKey.VolumeDown] = "VolumeDown",
        [VirtualKey.VolumeUp] = "VolumeUp",
        [VirtualKey.MediaPlayPause] = "PlayPause",
        [VirtualKey.MediaNextTrack] = "MediaNext",
        [VirtualKey.MediaPrevTrack] = "MediaPrev",
        [VirtualKey.MediaStop] = "MediaStop",
    }.ToFrozenDictionary();

    public string GetKeyName(int keyCode)
    {
        ushort vk = WindowsKeyMap.GetVirtualKey(keyCode);
        if (vk is 0)
        {
            return $"Key_{keyCode.ToString(CultureInfo.InvariantCulture)}";
        }

        if (FixedKeyNames.TryGetValue(vk, out var fixedName))
        {
            return fixedName;
        }

        uint scanCode = User32.MapVirtualKey(vk, User32.MAPVK_VK_TO_VSC);
        int lParam = (int)(scanCode << ScanCodeToLParamShift);

        if (WindowsKeyMap.NeedsExtendedFlag(keyCode))
        {
            lParam |= ExtendedKeyMask;
        }

        Span<char> buffer = stackalloc char[KeyNameBufferSize];
        int length = User32.GetKeyNameTextW(lParam, ref MemoryMarshal.GetReference(buffer), buffer.Length);

        return length > 0 ? new string(buffer[..length]) : vk.ToString(CultureInfo.InvariantCulture);
    }

    public int GetKeyCode(string keyName)
    {
        ArgumentNullException.ThrowIfNull(keyName);
        if (keyName.Length is 1)
        {
            var (vk, _, _) = GetVkForChar(keyName[0]);
            if (vk is not 0)
            {
                return WindowsKeyMap.GetEvdevCode((ushort)vk);
            }
        }
        return -1;
    }

    public char? GetCharFromKeyCode(int keyCode, bool leftShift, bool rightShift, bool rightAlt, bool leftAlt, bool leftCtrl, bool capsLock)
    {
        ushort vk = WindowsKeyMap.GetVirtualKey(keyCode);
        if (vk is 0)
        {
            return null;
        }

        uint scanCode = User32.MapVirtualKey(vk, User32.MAPVK_VK_TO_VSC);
        byte[] keyState = BuildKeyState(leftShift, rightShift, rightAlt, leftAlt, leftCtrl, capsLock);
        IntPtr layout = ResolveKeyboardLayout();

        Span<char> buffer = stackalloc char[UnicodeBufferSize];
        int result = User32.ToUnicodeEx(vk, scanCode, keyState, ref MemoryMarshal.GetReference(buffer), buffer.Length, 0, layout);

        return result > 0 ? buffer[0] : null;
    }

    private static byte[] BuildKeyState(bool leftShift, bool rightShift, bool rightAlt, bool leftAlt, bool leftCtrl, bool capsLock)
    {
        byte[] keyState = new byte[KeyStateSize];

        if (leftShift)
        {
            keyState[VirtualKey.Shift] = KeyDownState;
            keyState[VirtualKey.LeftShift] = KeyDownState;
        }

        if (rightShift)
        {
            keyState[VirtualKey.Shift] = KeyDownState;
            keyState[VirtualKey.RightShift] = KeyDownState;
        }

        if (leftCtrl)
        {
            keyState[VirtualKey.Control] = KeyDownState;
            keyState[VirtualKey.LeftControl] = KeyDownState;
        }

        if (leftAlt)
        {
            keyState[VirtualKey.Menu] = KeyDownState;
            keyState[VirtualKey.LeftMenu] = KeyDownState;
        }

        // AltGr reaches the layout as Ctrl+Alt with the right Alt key down.
        if (rightAlt)
        {
            keyState[VirtualKey.Control] = KeyDownState;
            keyState[VirtualKey.Menu] = KeyDownState;
            keyState[VirtualKey.LeftControl] = KeyDownState;
            keyState[VirtualKey.RightMenu] = KeyDownState;
        }

        if (capsLock)
        {
            keyState[VirtualKey.Capital] = KeyToggledState;
        }

        return keyState;
    }

    /// <summary>
    /// The layout of the foreground window's thread, which decides what typed text produces, falling back to this thread's layout
    /// when the foreground window's is unavailable. Used by every key-to-character and character-to-key lookup so the two directions agree.
    /// </summary>
    private static IntPtr ResolveKeyboardLayout()
    {
        IntPtr hwnd = User32.GetForegroundWindow();
        uint threadId = User32.GetWindowThreadProcessId(hwnd, IntPtr.Zero);
        IntPtr layout = User32.GetKeyboardLayout(threadId);
        return layout == IntPtr.Zero ? User32.GetKeyboardLayout(0) : layout;
    }

    public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c)
    {
        var result = GetVkForChar(c);
        if (result.vk is 0)
        {
            return null;
        }

        int evdev = WindowsKeyMap.GetEvdevCode((ushort)result.vk);
        if (evdev is 0)
        {
            return null;
        }

        return (evdev, result.shift, result.altGr);
    }

    private static (int vk, bool shift, bool altGr) GetVkForChar(char c)
    {
        short scanResult = User32.VkKeyScanEx(c, ResolveKeyboardLayout());

        if (scanResult == -1)
        {
            return (0, false, false);
        }

        int vk = scanResult & 0xFF;
        int shiftState = (scanResult >> 8) & 0xFF;

        bool shift = (shiftState & 1) is not 0;
        bool ctrl = (shiftState & 2) is not 0;
        bool alt = (shiftState & 4) is not 0;

        bool altGr = ctrl && alt;

        return (vk, shift, altGr);
    }
}
