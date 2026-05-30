using System;
using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Services;

internal enum MacOSKeyboardEventRoute
{
    Unsupported,
    Keyboard,
    SystemDefined
}

internal readonly struct MacOSSystemKeyEventPayload
{
    internal MacOSSystemKeyEventPayload(
        CoreGraphics.CGEventType eventType,
        CoreGraphics.CGEventFlags flags,
        long subtype,
        long data1,
        long data2)
    {
        EventType = eventType;
        Flags = flags;
        Subtype = subtype;
        Data1 = data1;
        Data2 = data2;
    }

    internal CoreGraphics.CGEventType EventType { get; }
    internal CoreGraphics.CGEventFlags Flags { get; }
    internal long Subtype { get; }
    internal long Data1 { get; }
    internal long Data2 { get; }
}

internal static class MacOSSystemKeyEventFactory
{
    internal static bool IsNSEventBridgeAvailable => MacOSSystemKeyNSEventBridge.IsAvailable;

    internal static bool TryGetNxKeyType(int keyCode, out int nxKeyType)
    {
        return MacOSSystemKeyMap.TryGetNxKeyType(keyCode, out nxKeyType);
    }

    internal static MacOSSystemKeyEventPayload CreatePayload(
        int nxKeyType,
        bool pressed,
        CoreGraphics.CGEventFlags activeModifierFlags = default)
    {
        return new MacOSSystemKeyEventPayload(
            CoreGraphics.CGEventType.SystemDefined,
            CreateEventFlags(pressed, activeModifierFlags),
            MacOSSystemKeyMap.NxSubtypeAuxControlButtons,
            CreateData1(nxKeyType, pressed),
            -1);
    }

    private static long CreateData1(int nxKeyType, bool pressed)
    {
        return (nxKeyType << 16) | (GetState(pressed) << 8);
    }

    private static CoreGraphics.CGEventFlags CreateEventFlags(
        bool pressed,
        CoreGraphics.CGEventFlags activeModifierFlags = default)
    {
        return activeModifierFlags | (CoreGraphics.CGEventFlags)(GetState(pressed) << 8);
    }

    internal static bool TryCreateEvent(
        int nxKeyType,
        bool pressed,
        long? marker,
        CoreGraphics.CGEventFlags activeModifierFlags,
        out IntPtr eventRef)
    {
        var payload = CreatePayload(nxKeyType, pressed, activeModifierFlags);
        if (MacOSSystemKeyNSEventBridge.TryCreateSystemDefinedCGEvent(payload, out eventRef))
        {
            ApplyMarker(eventRef, marker);
            return true;
        }

        // NSEvent.otherEvent(...).CGEvent is the preferred source shape for media/system keys.
        // Keep the direct CoreGraphics construction as a contained fallback for non-macOS CI,
        // older/unusual runtimes, and until fresh macOS evidence confirms the bridge path.
        eventRef = CoreGraphics.CGEventCreate(IntPtr.Zero);
        if (eventRef == IntPtr.Zero)
        {
            return false;
        }

        var releaseOnFailure = true;
        try
        {
            ApplyPayload(eventRef, payload);
            ApplyMarker(eventRef, marker);

            releaseOnFailure = false;
            return true;
        }
        finally
        {
            if (releaseOnFailure)
            {
                CoreFoundation.CFRelease(eventRef);
                eventRef = IntPtr.Zero;
            }
        }
    }

    private static void ApplyPayload(IntPtr eventRef, MacOSSystemKeyEventPayload payload)
    {
        CoreGraphics.CGEventSetType(eventRef, payload.EventType);
        CoreGraphics.CGEventSetFlags(eventRef, payload.Flags);
        CoreGraphics.CGEventSetIntegerValueField(eventRef, CoreGraphics.CGEventField.EventSubtype, payload.Subtype);
        CoreGraphics.CGEventSetIntegerValueField(eventRef, CoreGraphics.CGEventField.EventData1, payload.Data1);
        CoreGraphics.CGEventSetIntegerValueField(eventRef, CoreGraphics.CGEventField.EventData2, payload.Data2);
    }

    private static void ApplyMarker(IntPtr eventRef, long? marker)
    {
        if (marker.HasValue)
        {
            CoreGraphics.CGEventSetIntegerValueField(
                eventRef,
                CoreGraphics.CGEventField.EventSourceUserData,
                marker.Value);
        }
    }

    private static int GetState(bool pressed)
    {
        return pressed
            ? MacOSSystemKeyMap.SystemDefinedKeyDownState
            : MacOSSystemKeyMap.SystemDefinedKeyUpState;
    }
}
