namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandClipboardProtocolTests
{
    [Fact]
    public void CoreDataTransferInterfaces_MatchWaylandProtocolMetadata()
    {
        using var protocol = new WaylandClipboardProtocol();

        ReadMethodSignatures(protocol.WlDataDevice).Should().Equal("?oo?ou", "?ou", "2");
        ReadEventSignatures(protocol.WlDataDevice).Should().Equal("n", "uoff?o", "", "uff", "", "?o");
        ReadMethodSignatures(protocol.WlDataSource).Should().Equal("s", "", "3u");
        ReadEventSignatures(protocol.WlDataSource).Should().Equal("?s", "sh", "", "3", "3", "3u");
        ReadMethodSignatures(protocol.WlDataOffer).Should().Equal("u?s", "sh", "", "3", "3uu");
        ReadEventSignatures(protocol.WlDataOffer).Should().Equal("s", "3u", "3u");
    }

    [Fact]
    public void ExtDataControlDevice_AllowsNullClipboardSelections()
    {
        using var protocol = new WaylandClipboardProtocol();

        ReadMethodSignatures(protocol.ExtDataControlDevice).Should().Equal("?o", "", "?o");
        ReadEventSignatures(protocol.ExtDataControlDevice).Should().Equal("n", "?o", "", "?o");
    }

    [Fact]
    public void WlrDataControlDevice_AllowsNullClipboardSelections()
    {
        using var protocol = new WaylandClipboardProtocol();

        ReadMethodSignatures(protocol.WlrDataControlDevice).Should().Equal("?o", "", "2?o");
        ReadEventSignatures(protocol.WlrDataControlDevice).Should().Equal("n", "?o", "", "2?o");
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
