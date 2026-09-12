
namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandNativeStructLayoutTests
{
    [Fact]
    public void NativeStructs_HaveExpectedPointerSizedLayouts()
    {
        var pointerSize = IntPtr.Size;
        var methodsOffset = Align(pointerSize + (2 * sizeof(int)), pointerSize);
        var eventCountOffset = methodsOffset + pointerSize;
        var eventsOffset = Align(eventCountOffset + sizeof(int), pointerSize);

        AssertLayout<WlMessage>(
            pointerSize * 3,
            (nameof(WlMessage.Name), 0),
            (nameof(WlMessage.Signature), pointerSize),
            (nameof(WlMessage.Types), pointerSize * 2));
        AssertLayout<WlInterface>(
            Align(eventsOffset + pointerSize, pointerSize),
            (nameof(WlInterface.Name), 0),
            (nameof(WlInterface.Version), pointerSize),
            (nameof(WlInterface.MethodCount), pointerSize + sizeof(int)),
            (nameof(WlInterface.Methods), methodsOffset),
            (nameof(WlInterface.EventCount), eventCountOffset),
            (nameof(WlInterface.Events), eventsOffset));
        AssertLayout<WlArgument>(
            pointerSize,
            (nameof(WlArgument.i), 0),
            (nameof(WlArgument.u), 0),
            (nameof(WlArgument.s), 0),
            (nameof(WlArgument.o), 0),
            (nameof(WlArgument.h), 0));
    }

    [Fact]
    public void WlArgument_UnionValuesRoundTripThroughUnmanagedMemory()
    {
        var integer = new WlArgument { i = -42 };
        var unsigned = new WlArgument { u = 42 };
        var stringPointer = new WlArgument { s = new IntPtr(0x1234) };
        var objectPointer = new WlArgument { o = new IntPtr(0x5678) };
        var fileDescriptor = new WlArgument { h = 17 };

        Assert.Equal(integer.i, RoundTrip(integer).i);
        Assert.Equal(unsigned.u, RoundTrip(unsigned).u);
        Assert.Equal(stringPointer.s, RoundTrip(stringPointer).s);
        Assert.Equal(objectPointer.o, RoundTrip(objectPointer).o);
        Assert.Equal(fileDescriptor.h, RoundTrip(fileDescriptor).h);
    }

    [Fact]
    public void WlMessageAndWlInterface_PointerFieldsRoundTripThroughUnmanagedMemory()
    {
        var message = new WlMessage
        {
            Name = new IntPtr(0x1000),
            Signature = new IntPtr(0x2000),
            Types = new IntPtr(0x3000),
        };
        var iface = new WlInterface
        {
            Name = new IntPtr(0x4000),
            Version = 3,
            MethodCount = 2,
            Methods = new IntPtr(0x5000),
            EventCount = 1,
            Events = new IntPtr(0x6000),
        };

        var messageRoundTrip = RoundTrip(message);
        var interfaceRoundTrip = RoundTrip(iface);

        Assert.Equal(message.Name, messageRoundTrip.Name);
        Assert.Equal(message.Signature, messageRoundTrip.Signature);
        Assert.Equal(message.Types, messageRoundTrip.Types);
        Assert.Equal(iface.Name, interfaceRoundTrip.Name);
        Assert.Equal(iface.Version, interfaceRoundTrip.Version);
        Assert.Equal(iface.MethodCount, interfaceRoundTrip.MethodCount);
        Assert.Equal(iface.Methods, interfaceRoundTrip.Methods);
        Assert.Equal(iface.EventCount, interfaceRoundTrip.EventCount);
        Assert.Equal(iface.Events, interfaceRoundTrip.Events);
    }

    private static int Align(int offset, int alignment) =>
        (offset + alignment - 1) / alignment * alignment;

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
        where T : unmanaged
    {
        Span<T> storage = stackalloc T[1];
        Span<byte> bytes = MemoryMarshal.AsBytes(storage);
        MemoryMarshal.Write(bytes, in value);
        return MemoryMarshal.Read<T>(bytes);
    }
}
