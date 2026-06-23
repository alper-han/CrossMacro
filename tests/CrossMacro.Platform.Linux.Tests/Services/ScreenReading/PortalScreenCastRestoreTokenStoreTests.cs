using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using NSubstitute;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class PortalScreenCastRestoreTokenStoreTests
{
    [Fact]
    public void LoadRestoreToken_WhenStoredTokenIsBlank_ReturnsNull()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings { PortalScreenCastRestoreToken = " " });
        var store = new PortalScreenCastRestoreTokenStore(settings);

        var token = store.LoadRestoreToken();

        Assert.Null(token);
    }

    [Fact]
    public void LoadRestoreToken_WhenStoredTokenExists_ReturnsToken()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings { PortalScreenCastRestoreToken = "stored-token" });
        var store = new PortalScreenCastRestoreTokenStore(settings);

        var token = store.LoadRestoreToken();

        Assert.Equal("stored-token", token);
    }

    [Fact]
    public async Task SaveRestoreTokenAsync_WhenTokenIsBlank_DoesNotSave()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings());
        var store = new PortalScreenCastRestoreTokenStore(settings);

        await store.SaveRestoreTokenAsync(" ");

        await settings.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task SaveRestoreTokenAsync_WhenTokenIsUnchanged_DoesNotSave()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings { PortalScreenCastRestoreToken = "same-token" });
        var store = new PortalScreenCastRestoreTokenStore(settings);

        await store.SaveRestoreTokenAsync("same-token");

        await settings.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task SaveRestoreTokenAsync_WhenTokenChanged_PersistsAndSaves()
    {
        var current = new AppSettings { PortalScreenCastRestoreToken = "old-token" };
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(current);
        settings.SaveAsync().Returns(Task.CompletedTask);
        var store = new PortalScreenCastRestoreTokenStore(settings);

        await store.SaveRestoreTokenAsync("new-token");

        Assert.Equal("new-token", current.PortalScreenCastRestoreToken);
        await settings.Received(1).SaveAsync();
    }

    [Fact]
    public async Task ClearRestoreTokenAsync_WhenTokenIsBlank_DoesNotSave()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings { PortalScreenCastRestoreToken = " " });
        var store = new PortalScreenCastRestoreTokenStore(settings);

        await store.ClearRestoreTokenAsync();

        await settings.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ClearRestoreTokenAsync_WhenTokenExists_ClearsAndSaves()
    {
        var current = new AppSettings { PortalScreenCastRestoreToken = "stale-token" };
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(current);
        settings.SaveAsync().Returns(Task.CompletedTask);
        var store = new PortalScreenCastRestoreTokenStore(settings);

        await store.ClearRestoreTokenAsync();

        Assert.Null(current.PortalScreenCastRestoreToken);
        await settings.Received(1).SaveAsync();
    }
}
