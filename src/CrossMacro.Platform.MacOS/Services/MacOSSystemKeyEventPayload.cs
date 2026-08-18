
namespace CrossMacro.Platform.MacOS.Services;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct MacOSSystemKeyEventPayload
{
    internal MacOSSystemKeyEventPayload(
        CoreGraphics.CGEventType eventType,
        CoreGraphics.CGEventModifiers flags,
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
    internal CoreGraphics.CGEventModifiers Flags { get; }
    internal long Subtype { get; }
    internal long Data1 { get; }
    internal long Data2 { get; }
}
