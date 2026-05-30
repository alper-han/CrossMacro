using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.MacOS.Services;

internal static class MacOSSystemKeyMap
{
    internal const long NxSubtypeAuxControlButtons = 8;
    internal const int NxKeyTypeSoundUp = 0;
    internal const int NxKeyTypeSoundDown = 1;
    internal const int NxKeyTypeBrightnessUp = 2;
    internal const int NxKeyTypeBrightnessDown = 3;
    internal const int NxKeyTypeMute = 7;
    internal const int NxKeyTypePlay = 16;
    internal const int NxKeyTypeNext = 17;
    internal const int NxKeyTypePrevious = 18;
    internal const int NxKeyTypeFast = 19;
    internal const int NxKeyTypeRewind = 20;

    internal const int SystemDefinedKeyDownState = 0x0A;
    internal const int SystemDefinedKeyUpState = 0x0B;

    internal static bool TryGetInputEventCode(int nxKeyType, out int inputEventCode)
    {
        inputEventCode = nxKeyType switch
        {
            NxKeyTypeSoundUp => InputEventCode.KEY_VOLUMEUP,
            NxKeyTypeSoundDown => InputEventCode.KEY_VOLUMEDOWN,
            NxKeyTypeBrightnessUp => InputEventCode.KEY_BRIGHTNESSUP,
            NxKeyTypeBrightnessDown => InputEventCode.KEY_BRIGHTNESSDOWN,
            NxKeyTypeMute => InputEventCode.KEY_MUTE,
            NxKeyTypePlay => InputEventCode.KEY_PLAYPAUSE,
            NxKeyTypeNext => InputEventCode.KEY_NEXTSONG,
            NxKeyTypePrevious => InputEventCode.KEY_PREVIOUSSONG,
            NxKeyTypeFast => InputEventCode.KEY_FASTFORWARD,
            NxKeyTypeRewind => InputEventCode.KEY_REWIND,
            _ => -1
        };

        return inputEventCode != -1;
    }

    internal static bool TryGetNxKeyType(int inputEventCode, out int nxKeyType)
    {
        nxKeyType = inputEventCode switch
        {
            InputEventCode.KEY_VOLUMEUP => NxKeyTypeSoundUp,
            InputEventCode.KEY_VOLUMEDOWN => NxKeyTypeSoundDown,
            InputEventCode.KEY_BRIGHTNESSUP => NxKeyTypeBrightnessUp,
            InputEventCode.KEY_BRIGHTNESSDOWN => NxKeyTypeBrightnessDown,
            InputEventCode.KEY_MUTE => NxKeyTypeMute,
            InputEventCode.KEY_PLAYPAUSE => NxKeyTypePlay,
            InputEventCode.KEY_NEXTSONG => NxKeyTypeNext,
            InputEventCode.KEY_PREVIOUSSONG => NxKeyTypePrevious,
            InputEventCode.KEY_FASTFORWARD => NxKeyTypeFast,
            InputEventCode.KEY_REWIND => NxKeyTypeRewind,
            _ => -1
        };

        return nxKeyType != -1;
    }
}
