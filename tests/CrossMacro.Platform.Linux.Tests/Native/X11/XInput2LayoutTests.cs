
namespace CrossMacro.Platform.Linux.Tests.Native.X11;

public sealed class XInput2LayoutTests
{
    [Fact]
    public void XInput2Structs_HaveExpectedLayouts()
    {
        AssertLayout<XIEventMask>(16, (nameof(XIEventMask.DeviceId), 0), (nameof(XIEventMask.MaskLen), 4), (nameof(XIEventMask.Mask), 8));
        AssertLayout<XGenericEventCookie>(56,
            (nameof(XGenericEventCookie.type), 0), (nameof(XGenericEventCookie.serial), 8),
            (nameof(XGenericEventCookie.send_event), 16), (nameof(XGenericEventCookie.display), 24),
            (nameof(XGenericEventCookie.extension), 32), (nameof(XGenericEventCookie.evtype), 36),
            (nameof(XGenericEventCookie.cookie), 40), (nameof(XGenericEventCookie.data), 48));
        AssertLayout<XIValuatorState>(16, (nameof(XIValuatorState.mask_len), 0), (nameof(XIValuatorState.mask), 8));
        AssertLayout<XIRawEvent>(88,
            (nameof(XIRawEvent.type), 0), (nameof(XIRawEvent.serial), 8),
            (nameof(XIRawEvent.send_event), 16), (nameof(XIRawEvent.display), 24),
            (nameof(XIRawEvent.extension), 32), (nameof(XIRawEvent.evtype), 36),
            (nameof(XIRawEvent.time), 40), (nameof(XIRawEvent.deviceid), 48),
            (nameof(XIRawEvent.sourceid), 52), (nameof(XIRawEvent.detail), 56),
            (nameof(XIRawEvent.flags), 60), (nameof(XIRawEvent.valuators), 64),
            (nameof(XIRawEvent.raw_values), 80));
        AssertLayout<XIDeviceEvent>(192,
            (nameof(XIDeviceEvent.type), 0), (nameof(XIDeviceEvent.serial), 8),
            (nameof(XIDeviceEvent.send_event), 16), (nameof(XIDeviceEvent.display), 24),
            (nameof(XIDeviceEvent.extension), 32), (nameof(XIDeviceEvent.evtype), 36),
            (nameof(XIDeviceEvent.time), 40), (nameof(XIDeviceEvent.deviceid), 48),
            (nameof(XIDeviceEvent.sourceid), 52), (nameof(XIDeviceEvent.detail), 56),
            (nameof(XIDeviceEvent.root), 64), (nameof(XIDeviceEvent.event_window), 72),
            (nameof(XIDeviceEvent.child), 80), (nameof(XIDeviceEvent.root_x), 88),
            (nameof(XIDeviceEvent.root_y), 96), (nameof(XIDeviceEvent.event_x), 104),
            (nameof(XIDeviceEvent.event_y), 112), (nameof(XIDeviceEvent.flags), 120),
            (nameof(XIDeviceEvent.buttons), 128), (nameof(XIDeviceEvent.valuators), 144),
            (nameof(XIDeviceEvent.mods), 160), (nameof(XIDeviceEvent.group), 176));
        AssertLayout<XIModifierState>(16,
            (nameof(XIModifierState.@base), 0), (nameof(XIModifierState.latched), 4),
            (nameof(XIModifierState.locked), 8), (nameof(XIModifierState.effective), 12));

        Assert.Equal(192, Marshal.SizeOf<XEvent>());
        Assert.Equal(0, Marshal.OffsetOf<XEvent>(nameof(XEvent.type)).ToInt32());
        Assert.Equal(0, Marshal.OffsetOf<XEvent>(nameof(XEvent.xcookie)).ToInt32());
        Assert.Equal(Marshal.SizeOf<XEvent>(), XInput2Consts.XEVENT_STRUCT_SIZE);
    }

    [Fact]
    public void XInput2Structs_RoundTripThroughUnmanagedMemory()
    {
        var eventMask = new XIEventMask { DeviceId = -3, MaskLen = 7, Mask = Pointer(0x1000) };
        var cookie = new XGenericEventCookie
        {
            type = 35, serial = Pointer(0x12345678), send_event = true, display = Pointer(0x2000),
            extension = -2, evtype = 6, cookie = 99, data = Pointer(0x3000),
        };
        var valuators = new XIValuatorState { mask_len = 11, mask = Pointer(0x4000) };
        var rawEvent = new XIRawEvent
        {
            type = 35, serial = Pointer(0x123456789abcdef0), send_event = true, display = Pointer(0x5000),
            extension = 2, evtype = 17, time = Pointer(0x6000), deviceid = -1, sourceid = 4,
            detail = 8, flags = 16, valuators = valuators, raw_values = Pointer(0x7000),
        };
        var deviceEvent = new XIDeviceEvent
        {
            type = 35, serial = Pointer(0x23456789abcdef01), send_event = true, display = Pointer(0x8000),
            extension = 2, evtype = 6, time = Pointer(0x9000), deviceid = 2, sourceid = 3, detail = 4,
            root = Pointer(0xa000), event_window = Pointer(0xb000), child = Pointer(0xc000),
            root_x = 1.25, root_y = -2.5, event_x = 3.75, event_y = -4.125, flags = 5,
            buttons = new XIValuatorState { mask_len = 6, mask = Pointer(0xd000) },
            valuators = new XIValuatorState { mask_len = 7, mask = Pointer(0xe000) },
            mods = new XIModifierState { @base = 8, latched = 9, locked = 10, effective = 11 },
            group = new XIModifierState { @base = 12, latched = 13, locked = 14, effective = 15 },
        };

        Assert.Equal(eventMask, RoundTrip(eventMask));
        Assert.Equal(cookie, RoundTrip(cookie));
        Assert.Equal(valuators, RoundTrip(valuators));
        Assert.Equal(rawEvent, RoundTrip(rawEvent));
        Assert.Equal(deviceEvent, RoundTrip(deviceEvent));
    }

    [Fact]
    public void XEvent_RoundTripsUnionTypeAndCookie()
    {
        var typeEvent = new XEvent { type = 35 };
        var cookieEvent = new XEvent
        {
            xcookie = new XGenericEventCookie { type = 35, serial = Pointer(0x1111), send_event = true, data = Pointer(0x2222) },
        };

        Assert.Equal(typeEvent.type, RoundTrip(typeEvent).type);
        Assert.Equal(cookieEvent.xcookie, RoundTrip(cookieEvent).xcookie);
    }

    private static void AssertLayout<T>(int size, params (string Field, int Offset)[] fields)
        where T : struct
    {
        Assert.Equal(size, Marshal.SizeOf<T>());
        foreach (var (field, offset) in fields)
        {
            Assert.Equal(offset, Marshal.OffsetOf<T>(field).ToInt32());
        }
    }

    private static T RoundTrip<T>(T value)
        where T : struct
    {
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        try
        {
            Marshal.StructureToPtr(value, pointer, fDeleteOld: false);
            return Marshal.PtrToStructure<T>(pointer);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private static IntPtr Pointer(ulong value)
    {
        return new IntPtr(unchecked((long)value));
    }
}
