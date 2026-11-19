
namespace CrossMacro.Platform.MacOS.Services;

internal static class MacOSSystemKeyEventFactory
{
    internal static MacOSSystemKeyEventPayload CreatePayload(
        int nxKeyType,
        bool pressed,
        CoreGraphics.CGEventModifiers activeModifierFlags = default)
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

    private static CoreGraphics.CGEventModifiers CreateEventFlags(
        bool pressed,
        CoreGraphics.CGEventModifiers activeModifierFlags = default)
    {
        return activeModifierFlags | (CoreGraphics.CGEventModifiers)(GetState(pressed) << 8);
    }

    internal static bool TryCreateEvent(
        int nxKeyType,
        bool pressed,
        long? marker,
        CoreGraphics.CGEventModifiers activeModifierFlags,
        out IntPtr eventRef)
    {
        var payload = CreatePayload(nxKeyType, pressed, activeModifierFlags);
        if (MacOSSystemKeyNSEventBridge.TryCreateSystemDefinedCGEvent(payload, out eventRef))
        {
            ApplyMarker(eventRef, marker);
            return true;
        }

        // NSEvent.otherEvent(...).CGEvent is the preferred source for media/system keys.
        // When AppKit is unavailable or does not return an event, build the CGEvent directly.
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
        if (marker is not null)
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
