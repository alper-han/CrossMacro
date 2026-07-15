
namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandNativeStructLayoutTests
{
    [Fact]
    public void NativeStructs_HaveExpectedX64Layouts()
    {
        AssertLayout<WlMessage>(
            24,
            (nameof(WlMessage.Name), 0),
            (nameof(WlMessage.Signature), 8),
            (nameof(WlMessage.Types), 16));
        AssertLayout<WlInterface>(
            40,
            (nameof(WlInterface.Name), 0),
            (nameof(WlInterface.Version), 8),
            (nameof(WlInterface.MethodCount), 12),
            (nameof(WlInterface.Methods), 16),
            (nameof(WlInterface.EventCount), 24),
            (nameof(WlInterface.Events), 32));
        AssertLayout<WlArgument>(
            8,
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
}
