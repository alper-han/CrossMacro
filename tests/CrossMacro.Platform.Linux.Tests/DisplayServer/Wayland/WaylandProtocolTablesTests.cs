namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandProtocolTablesTests
{
    [Fact]
    public void Interfaces_PreserveNamesVersionsAndMessageCounts()
    {
        using var protocol = new WaylandProtocolTables();

        AssertInterface(protocol.WlRegistry, "wl_registry", 1, 1, 2);
        AssertInterface(protocol.WlSeat, "wl_seat", 1, 3, 2);
        AssertInterface(protocol.WlPointer, "wl_pointer", 1, 1, 5);
        AssertInterface(protocol.WlOutput, "wl_output", 4, 0, 6);
        AssertInterface(protocol.WlShm, "wl_shm", 1, 1, 1);
        AssertInterface(protocol.WlShmPool, "wl_shm_pool", 1, 3, 0);
        AssertInterface(protocol.WlBuffer, "wl_buffer", 1, 1, 1);
        AssertInterface(protocol.XdgOutputManager, "zxdg_output_manager_v1", 3, 2, 0);
        AssertInterface(protocol.XdgOutput, "zxdg_output_v1", 3, 1, 5);
        AssertInterface(protocol.ExtOutputSourceManager, "ext_output_image_capture_source_manager_v1", 1, 2, 0);
        AssertInterface(protocol.ExtCaptureSource, "ext_image_capture_source_v1", 1, 1, 0);
        AssertInterface(protocol.ExtCopyManager, "ext_image_copy_capture_manager_v1", 1, 3, 0);
        AssertInterface(protocol.ExtCursorSession, "ext_image_copy_capture_cursor_session_v1", 1, 2, 4);
        AssertInterface(protocol.ExtCopySession, "ext_image_copy_capture_session_v1", 1, 2, 6);
        AssertInterface(protocol.ExtCopyFrame, "ext_image_copy_capture_frame_v1", 1, 4, 5);
        AssertInterface(protocol.WlrScreencopyManager, "zwlr_screencopy_manager_v1", 3, 3, 0);
        AssertInterface(protocol.WlrScreencopyFrame, "zwlr_screencopy_frame_v1", 3, 3, 7);
    }

    [Fact]
    public void Interfaces_PreserveMessageSignaturesAndTypedInterfacePointers()
    {
        using var protocol = new WaylandProtocolTables();

        _ = ReadMethodSignatures(protocol.WlShm).Should().Equal("nhi");
        _ = ReadMethodSignatures(protocol.ExtCopyManager).Should().Equal("nou", "noo", "");
        _ = ReadMethodSignatures(protocol.WlrScreencopyManager).Should().Equal("nuo", "nuoiiii", "");

        _ = ReadMethodTypes(protocol.WlSeat, 0).Should().Equal(protocol.WlPointer.Address);
        _ = ReadMethodTypes(protocol.WlShm, 0).Should().Equal(protocol.WlShmPool.Address, IntPtr.Zero, IntPtr.Zero);
        _ = ReadMethodTypes(protocol.XdgOutputManager, 1).Should().Equal(protocol.XdgOutput.Address, protocol.WlOutput.Address);
        _ = ReadMethodTypes(protocol.ExtCopyManager, 0).Should().Equal(protocol.ExtCopySession.Address, protocol.ExtCaptureSource.Address, IntPtr.Zero);
        _ = ReadMethodTypes(protocol.ExtCopyManager, 1).Should().Equal(protocol.ExtCursorSession.Address, protocol.ExtCaptureSource.Address, protocol.WlPointer.Address);
        _ = ReadMethodTypes(protocol.WlrScreencopyManager, 1).Should().Equal(
            protocol.WlrScreencopyFrame.Address,
            IntPtr.Zero,
            protocol.WlOutput.Address,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
        _ = ReadMethodTypes(protocol.WlrScreencopyFrame, 0).Should().Equal(protocol.WlBuffer.Address);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var protocol = new WaylandProtocolTables();
        Assert.NotEqual(IntPtr.Zero, protocol.WlRegistry.Address);

        protocol.Dispose();
        protocol.Dispose();
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

    private static string[] ReadMethodSignatures(WaylandInterfaceHandle interfaceHandle)
    {
        var definition = Marshal.PtrToStructure<WlInterface>(interfaceHandle.Address);
        var signatures = new string[definition.MethodCount];
        var messageSize = Marshal.SizeOf<WlMessage>();
        for (var index = 0; index < definition.MethodCount; index++)
        {
            var message = Marshal.PtrToStructure<WlMessage>(IntPtr.Add(definition.Methods, index * messageSize));
            signatures[index] = Marshal.PtrToStringUTF8(message.Signature) ?? string.Empty;
        }

        return signatures;
    }

    private static IntPtr[] ReadMethodTypes(WaylandInterfaceHandle interfaceHandle, int methodIndex)
    {
        var definition = Marshal.PtrToStructure<WlInterface>(interfaceHandle.Address);
        var message = Marshal.PtrToStructure<WlMessage>(
            IntPtr.Add(definition.Methods, methodIndex * Marshal.SizeOf<WlMessage>()));
        var signature = Marshal.PtrToStringUTF8(message.Signature) ?? string.Empty;
        var argumentCount = signature.Count(static character => character is 'i' or 'u' or 'f' or 's' or 'o' or 'n' or 'a' or 'h');
        if (argumentCount is 0)
        {
            return [];
        }

        var types = new IntPtr[argumentCount];
        Marshal.Copy(message.Types, types, 0, argumentCount);
        return types;
    }
}
