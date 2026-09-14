namespace CrossMacro.Platform.Linux.Native.Evdev;

/// <summary>
/// Lazily snapshots each capability bitmap once during one open-fd inspection.
/// A new device inspection creates a new instance; this is not a hotplug cache.
/// </summary>
internal sealed class EvdevDeviceCapabilities(Func<int, byte[], int> readBitmap)
{
    internal const int MaximumKeyCode = 767;
    private const int KeyEscape = 1;
    private const int KeyEnter = 28;
    private const int FirstLetterKey = 30;
    private const int LastLetterKey = 44;
    private const int BitsPerByte = 8;
    private const int CapabilityMaskBytes = (MaximumKeyCode + 1) / BitsPerByte;
    private readonly Func<int, byte[], int> _readBitmap = readBitmap ?? throw new ArgumentNullException(nameof(readBitmap));
    private readonly Dictionary<int, byte[]> _bitmaps = [];

    internal bool HasCapability(int eventType, int code)
    {
        if (code is < 0 or > MaximumKeyCode)
        {
            return false;
        }

        if (!_bitmaps.TryGetValue(eventType, out var bitmap))
        {
            bitmap = new byte[CapabilityMaskBytes];
            if (_readBitmap(eventType, bitmap) < 0)
            {
                bitmap = [];
            }
            _bitmaps.Add(eventType, bitmap);
        }

        var byteIndex = code / BitsPerByte;
        return byteIndex < bitmap.Length && (bitmap[byteIndex] & (1 << (code % BitsPerByte))) is not 0;
    }

    internal bool IsMouse()
    {
        if (!HasCapability(UInputNative.EV_SYN, UInputNative.EV_REL)
            || !HasCapability(UInputNative.EV_SYN, UInputNative.EV_KEY)
            || !HasCapability(UInputNative.EV_REL, UInputNative.REL_X)
            || !HasCapability(UInputNative.EV_REL, UInputNative.REL_Y))
        {
            return false;
        }

        for (var button = (int)UInputNative.BTN_LEFT; button <= UInputNative.BTN_TASK; button++)
        {
            if (HasCapability(UInputNative.EV_KEY, button))
            {
                return true;
            }
        }
        return false;
    }

    internal bool IsTouchpad()
    {
        if (!HasCapability(UInputNative.EV_SYN, UInputNative.EV_ABS)
            || !HasCapability(UInputNative.EV_SYN, UInputNative.EV_KEY))
        {
            return false;
        }

        var hasButton = HasCapability(UInputNative.EV_KEY, UInputNative.BTN_TOUCH)
                        || HasCapability(UInputNative.EV_KEY, UInputNative.BTN_LEFT);
        if (!hasButton)
        {
            return false;
        }

        var hasPosition = (HasCapability(UInputNative.EV_ABS, UInputNative.ABS_X) && HasCapability(UInputNative.EV_ABS, UInputNative.ABS_Y))
                          || (HasCapability(UInputNative.EV_ABS, UInputNative.ABS_MT_POSITION_X) && HasCapability(UInputNative.EV_ABS, UInputNative.ABS_MT_POSITION_Y));
        return hasPosition && !HasCapability(UInputNative.EV_SYN, UInputNative.EV_REL);
    }

    internal bool IsKeyboard()
    {
        if (!HasCapability(UInputNative.EV_SYN, UInputNative.EV_KEY)
            || (!HasCapability(UInputNative.EV_KEY, KeyEscape) && !HasCapability(UInputNative.EV_KEY, KeyEnter)))
        {
            return false;
        }

        for (var key = FirstLetterKey; key <= LastLetterKey; key++)
        {
            if (HasCapability(UInputNative.EV_KEY, key))
            {
                return true;
            }
        }
        return false;
    }
}
