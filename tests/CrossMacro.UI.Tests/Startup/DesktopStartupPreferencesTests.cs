using CrossMacro.Core.Models;
using CrossMacro.UI.Startup;
using FluentAssertions;

namespace CrossMacro.UI.Tests.Startup;

public sealed class DesktopStartupPreferencesTests
{
    [Fact]
    public void Resolve_WhenNoStartupFlags_ReturnsVisibleStartupWithoutTray()
    {
        var preferences = DesktopStartupPreferences.Resolve(
            new AppSettings { EnableTrayIcon = false, StartMinimized = false },
            GuiStartupOptions.Default);

        preferences.ShouldStartMinimized.Should().BeFalse();
        preferences.PersistTrayEnabled.Should().BeFalse();
        preferences.UseStartupTrayOnly.Should().BeFalse();
        preferences.ShouldEnableTrayDuringStartup.Should().BeFalse();
        preferences.ResolveDisplayMode(trayAvailable: true).Should().Be(DesktopStartupDisplayMode.Visible);
    }

    [Fact]
    public void Resolve_WhenCliRequestsMinimizedStartup_UsesStartupOnlyTray()
    {
        var preferences = DesktopStartupPreferences.Resolve(
            new AppSettings { EnableTrayIcon = false, StartMinimized = false },
            new GuiStartupOptions(StartMinimized: true));

        preferences.ShouldStartMinimized.Should().BeTrue();
        preferences.PersistTrayEnabled.Should().BeFalse();
        preferences.UseStartupTrayOnly.Should().BeTrue();
        preferences.ShouldEnableTrayDuringStartup.Should().BeTrue();
        preferences.ResolveDisplayMode(trayAvailable: true).Should().Be(DesktopStartupDisplayMode.HiddenToTray);
        preferences.ResolveDisplayMode(trayAvailable: false).Should().Be(DesktopStartupDisplayMode.Minimized);
    }

    [Fact]
    public void Resolve_WhenPersistedStartMinimizedIsEnabled_KeepsTrayPersistent()
    {
        var preferences = DesktopStartupPreferences.Resolve(
            new AppSettings { EnableTrayIcon = false, StartMinimized = true },
            GuiStartupOptions.Default);

        preferences.ShouldStartMinimized.Should().BeTrue();
        preferences.PersistTrayEnabled.Should().BeTrue();
        preferences.UseStartupTrayOnly.Should().BeFalse();
        preferences.ShouldEnableTrayDuringStartup.Should().BeTrue();
        preferences.ResolveDisplayMode(trayAvailable: true).Should().Be(DesktopStartupDisplayMode.HiddenToTray);
    }
}
