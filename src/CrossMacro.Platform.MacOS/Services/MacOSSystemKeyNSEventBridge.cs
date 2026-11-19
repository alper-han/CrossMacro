namespace CrossMacro.Platform.MacOS.Services;

internal static partial class MacOSSystemKeyNSEventBridge
{
    private static readonly Lazy<IntPtr> NSEventClass = new(() => ObjCRuntime.GetClass("NSEvent"));
    private static readonly Lazy<bool> BridgeAvailable = new(() => ObjCRuntime.IsAvailable && NSEventClass.Value != IntPtr.Zero);
    private static readonly Lazy<IntPtr> OtherEventSelector = new(() => ObjCRuntime.RegisterSelector("otherEventWithType:location:modifierFlags:timestamp:windowNumber:context:subtype:data1:data2:"));
    private static readonly Lazy<IntPtr> CGEventSelector = new(() => ObjCRuntime.RegisterSelector("CGEvent"));

    internal static bool IsAvailable => BridgeAvailable.Value;

    internal static bool TryCreateSystemDefinedCGEvent(
        MacOSSystemKeyEventPayload payload,
        out IntPtr eventRef)
    {
        eventRef = IntPtr.Zero;
        if (!IsAvailable)
        {
            return false;
        }

        using var pool = ObjCRuntime.PushAutoreleasePool();
        var nsEvent = objc_msgSend_otherEventWithType(
            NSEventClass.Value,
            OtherEventSelector.Value,
            (nuint)payload.EventType,
            new NSPoint(),
            (nuint)payload.Flags,
            timestamp: 0,
            windowNumber: 0,
            context: IntPtr.Zero,
            subtype: (short)payload.Subtype,
            data1: (nint)payload.Data1,
            data2: (nint)payload.Data2);
        if (nsEvent == IntPtr.Zero)
        {
            return false;
        }

        var cgEvent = ObjCRuntime.SendIntPtr(nsEvent, CGEventSelector.Value);
        if (cgEvent == IntPtr.Zero)
        {
            return false;
        }

        eventRef = CoreFoundation.CFRetain(cgEvent);
        return eventRef != IntPtr.Zero;
    }

    [LibraryImport(ObjCRuntime.Library, EntryPoint = "objc_msgSend")]
    private static partial IntPtr objc_msgSend_otherEventWithType(
        IntPtr receiver,
        IntPtr selector,
        nuint type,
        NSPoint location,
        nuint modifierFlags,
        double timestamp,
        nint windowNumber,
        IntPtr context,
        short subtype,
        nint data1,
        nint data2);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NSPoint
    {
        private readonly double _x;
        private readonly double _y;
    }
}
