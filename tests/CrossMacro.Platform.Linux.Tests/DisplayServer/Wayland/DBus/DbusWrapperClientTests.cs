using System.Collections.Generic;
using CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;
using CrossMacro.TestInfrastructure;

namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.DBus;

public class DbusWrapperClientTests
{
    [LinuxFact]
    public void DbusWrapper_KWinScriptingScalarReply_ShouldParseScriptId()
    {
        var reply = DbusWrapperProtocolTestHelpers.CreateBodyOnlyMessage(
            DbusWrapperProtocolTestHelpers.EncodeInt32Body(42));

        Assert.Equal(42, KWinScriptingClient.ReadLoadScriptReply(reply, null));
    }

    [LinuxFact]
    public void DbusWrapper_KdeKeyboardArrayReply_ShouldParseLayouts()
    {
        var reply = DbusWrapperProtocolTestHelpers.CreateBodyOnlyMessage(
            DbusWrapperProtocolTestHelpers.EncodeLayoutTriplesBody(
                ("us", string.Empty, "English (US)"),
                ("de", "nodeadkeys", "German")));

        var layouts = KdeKeyboardClient.ReadGetLayoutsListReply(reply, null);

        Assert.Collection(
            layouts,
            layout => Assert.Equal(("us", string.Empty, "English (US)"), layout),
            layout => Assert.Equal(("de", "nodeadkeys", "German"), layout));
    }

    [LinuxFact]
    public void DbusWrapper_GnomeExtensionsDictionaryReply_ShouldParseVariantValues()
    {
        var reply = DbusWrapperProtocolTestHelpers.CreateBodyOnlyMessage(
            DbusWrapperProtocolTestHelpers.EncodeStringVariantDictionaryBody(
                ("state", (object)(uint)1),
                ("name", "Cursor Spy"),
                ("enabled", true)));

        var info = GnomeShellExtensionsClient.ReadGetExtensionInfoReply(reply, null);

        Assert.Equal((uint)1, info["state"]);
        Assert.Equal("Cursor Spy", info["name"]);
        Assert.Equal(true, info["enabled"]);
    }
}
