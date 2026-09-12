namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandClipboardProtocolTests
{
    [Fact]
    public void CoreDataTransferInterfaces_MatchWaylandProtocolMetadata()
    {
        using var protocol = new WaylandClipboardProtocol();

        _ = ReadMethodSignatures(protocol.WlDataDevice).Should().Equal("?oo?ou", "?ou", "2");
        _ = ReadEventSignatures(protocol.WlDataDevice).Should().Equal("n", "uoff?o", "", "uff", "", "?o");
        _ = ReadMethodSignatures(protocol.WlDataSource).Should().Equal("s", "", "3u");
        _ = ReadEventSignatures(protocol.WlDataSource).Should().Equal("?s", "sh", "", "3", "3", "3u");
        _ = ReadMethodSignatures(protocol.WlDataOffer).Should().Equal("u?s", "sh", "", "3", "3uu");
        _ = ReadEventSignatures(protocol.WlDataOffer).Should().Equal("s", "3u", "3u");
    }

    [Fact]
    public void ExtDataControlDevice_AllowsNullClipboardSelections()
    {
        using var protocol = new WaylandClipboardProtocol();

        _ = ReadMethodSignatures(protocol.ExtDataControlDevice).Should().Equal("?o", "", "?o");
        _ = ReadEventSignatures(protocol.ExtDataControlDevice).Should().Equal("n", "?o", "", "?o");
    }

    [Fact]
    public void WlrDataControlDevice_AllowsNullClipboardSelections()
    {
        using var protocol = new WaylandClipboardProtocol();

        _ = ReadMethodSignatures(protocol.WlrDataControlDevice).Should().Equal("?o", "", "2?o");
        _ = ReadEventSignatures(protocol.WlrDataControlDevice).Should().Equal("n", "?o", "", "2?o");
    }

    [Fact]
    public void InterfaceMetadata_ShouldPreserveNamesVersionsCountsAndIdempotentDisposal()
    {
        using var protocol = new WaylandClipboardProtocol();

        AssertInterface(protocol.WlRegistry, "wl_registry", 1, 1, 2);
        AssertInterface(protocol.WlSeat, "wl_seat", 7, 4, 2);
        AssertInterface(protocol.WlDataDevice, "wl_data_device", 3, 3, 6);
        AssertInterface(protocol.ExtDataControlManager, "ext_data_control_manager_v1", 1, 3, 0);
        AssertInterface(protocol.WlrDataControlManager, "zwlr_data_control_manager_v1", 2, 3, 0);
        AssertInterface(protocol.XdgWmBase, "xdg_wm_base", 1, 4, 1);

        protocol.Dispose();
        protocol.Dispose();
    }

    [Fact]
    public void WlCString_PreservesUtf8NulTerminatorAndDisposesIdempotently()
    {
        using var value = new WlCString("Wayland✓");

        Assert.Equal("Wayland✓", Marshal.PtrToStringUTF8(value.Address));

        value.Dispose();
        value.Dispose();
    }

    [Fact]
    public void WlArgumentPack_PinsIndexedArgumentsAndDisposesIdempotently()
    {
        using var arguments = new WlArgumentPack(2);
        arguments[0] = new WlArgument { i = -42 };
        arguments[1] = new WlArgument { u = 42 };

        var first = Marshal.PtrToStructure<WlArgument>(arguments.Address);
        var second = Marshal.PtrToStructure<WlArgument>(IntPtr.Add(arguments.Address, Marshal.SizeOf<WlArgument>()));

        Assert.Equal(-42, first.i);
        Assert.Equal((uint)42, second.u);

        arguments.Dispose();
        arguments.Dispose();
    }

    private static string[] ReadMethodSignatures(WaylandInterfaceHandle interfaceHandle)
    {
        var definition = Marshal.PtrToStructure<WlInterface>(interfaceHandle.Address);
        return ReadSignatures(definition.Methods, definition.MethodCount);
    }

    private static string[] ReadEventSignatures(WaylandInterfaceHandle interfaceHandle)
    {
        var definition = Marshal.PtrToStructure<WlInterface>(interfaceHandle.Address);
        return ReadSignatures(definition.Events, definition.EventCount);
    }

    private static void AssertInterface(
        WaylandInterfaceHandle interfaceHandle,
        string expectedName,
        int expectedVersion,
        int expectedMethodCount,
        int expectedEventCount)
    {
        var definition = Marshal.PtrToStructure<WlInterface>(interfaceHandle.Address);
        Assert.Equal(expectedName, Marshal.PtrToStringUTF8(definition.Name));
        Assert.Equal(expectedVersion, definition.Version);
        Assert.Equal(expectedMethodCount, definition.MethodCount);
        Assert.Equal(expectedEventCount, definition.EventCount);
    }

    private static string[] ReadSignatures(IntPtr messages, int messageCount)
    {
        var signatures = new string[messageCount];
        var messageSize = Marshal.SizeOf<WlMessage>();
        for (var index = 0; index < messageCount; index++)
        {
            var messageAddress = IntPtr.Add(messages, index * messageSize);
            var message = Marshal.PtrToStructure<WlMessage>(messageAddress);
            signatures[index] = Marshal.PtrToStringUTF8(message.Signature) ?? string.Empty;
        }

        return signatures;
    }
}
