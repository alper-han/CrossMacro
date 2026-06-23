using CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class PortalScreenCastClientRestoreTokenTests
{
    [Fact]
    public void BuildSelectSourcesOptions_WhenRestoreTokenMissing_RequestsPersistentSessionWithoutRestoreToken()
    {
        var options = PortalScreenCastClient.BuildSelectSourcesOptions("handle-token", " ");

        Assert.Equal(1U, options["types"].GetUInt32());
        Assert.True(options["multiple"].GetBool());
        Assert.Equal(1U, options["cursor_mode"].GetUInt32());
        Assert.Equal(2U, options["persist_mode"].GetUInt32());
        Assert.Equal("handle-token", options["handle_token"].GetString());
        Assert.False(options.ContainsKey("restore_token"));
    }

    [Fact]
    public void BuildSelectSourcesOptions_WhenRestoreTokenExists_IncludesRestoreToken()
    {
        var options = PortalScreenCastClient.BuildSelectSourcesOptions("handle-token", "stored-token");

        Assert.Equal(2U, options["persist_mode"].GetUInt32());
        Assert.Equal("stored-token", options["restore_token"].GetString());
    }

    [Fact]
    public void TryGetResponseString_WhenRestoreTokenIsReturned_ReadsToken()
    {
        var results = new Dictionary<string, VariantValue>
        {
            ["restore_token"] = VariantValue.String("next-token")
        };

        var token = PortalScreenCastClient.TryGetResponseString(results, "restore_token");

        Assert.Equal("next-token", token);
    }

    [Fact]
    public void TryGetResponseString_WhenRestoreTokenIsMissing_ReturnsNull()
    {
        var token = PortalScreenCastClient.TryGetResponseString(
            new Dictionary<string, VariantValue>(),
            "restore_token");

        Assert.Null(token);
    }
}
