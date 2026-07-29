
namespace CrossMacro.UI.Tests.Startup;

public sealed class DesktopStartupPreferencesTests
{
    [Fact]
    public void Resolve_WhenNoStartupFlags_ReturnsVisibleStartupWithoutTray()
    {
        var preferences = DesktopStartupPreferences.Resolve(
            new AppSettings { EnableTrayIcon = false, StartMinimized = false },
            GuiStartupOptions.Default);

        _ = preferences.ShouldStartMinimized.Should().BeFalse();
        _ = preferences.PersistTrayEnabled.Should().BeFalse();
        _ = preferences.UseStartupTrayOnly.Should().BeFalse();
        _ = preferences.ShouldEnableTrayDuringStartup.Should().BeFalse();
        _ = preferences.ResolveDisplayMode(trayAvailable: true).Should().Be(DesktopStartupDisplayMode.Visible);
    }

    [Fact]
    public void Resolve_WhenCliRequestsMinimizedStartup_UsesStartupOnlyTray()
    {
        var preferences = DesktopStartupPreferences.Resolve(
            new AppSettings { EnableTrayIcon = false, StartMinimized = false },
            new GuiStartupOptions(StartMinimized: true));

        _ = preferences.ShouldStartMinimized.Should().BeTrue();
        _ = preferences.PersistTrayEnabled.Should().BeFalse();
        _ = preferences.UseStartupTrayOnly.Should().BeTrue();
        _ = preferences.ShouldEnableTrayDuringStartup.Should().BeTrue();
        _ = preferences.ResolveDisplayMode(trayAvailable: true).Should().Be(DesktopStartupDisplayMode.HiddenToTray);
        _ = preferences.ResolveDisplayMode(trayAvailable: false).Should().Be(DesktopStartupDisplayMode.Minimized);
    }

    [Fact]
    public void Resolve_WhenPersistedStartMinimizedIsEnabled_KeepsTrayPersistent()
    {
        var preferences = DesktopStartupPreferences.Resolve(
            new AppSettings { EnableTrayIcon = false, StartMinimized = true },
            GuiStartupOptions.Default);

        _ = preferences.ShouldStartMinimized.Should().BeTrue();
        _ = preferences.PersistTrayEnabled.Should().BeTrue();
        _ = preferences.UseStartupTrayOnly.Should().BeFalse();
        _ = preferences.ShouldEnableTrayDuringStartup.Should().BeTrue();
        _ = preferences.ResolveDisplayMode(trayAvailable: true).Should().Be(DesktopStartupDisplayMode.HiddenToTray);
    }
}
