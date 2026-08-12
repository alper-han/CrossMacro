namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class PortalScreenCastProviderDiagnosticTests
{
    [Fact]
    public void Describe_WhenDesktopConfigSelectsScreenCastProvider_ReportsProviderWithoutAbsolutePath()
    {
        var environment = CreateEnvironment("Hyprland");
        var config = "/config/xdg-desktop-portal/hyprland-portals.conf";

        var result = PortalScreenCastProviderDiagnostic.Describe(
            environment,
            path => string.Equals(path, config, StringComparison.Ordinal),
            path => string.Equals(path, config, StringComparison.Ordinal)
                ? "[preferred]\norg.freedesktop.impl.portal.ScreenCast=hyprland;wlr"
                : null);

        Assert.Contains("hyprland", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hyprland-portals.conf", result, StringComparison.Ordinal);
        Assert.DoesNotContain("/config/", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_WhenConfigHasNoScreenCastOverride_ReportsExternalSelection()
    {
        var environment = CreateEnvironment("GNOME");

        var result = PortalScreenCastProviderDiagnostic.Describe(
            environment,
            path => string.Equals(path, "/config/xdg-desktop-portal/portals.conf", StringComparison.Ordinal),
            _ => "[preferred]\norg.freedesktop.impl.portal.Settings=gnome");

        Assert.Contains("no explicit ScreenCast provider override", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_WhenConfigSelectsGtk_ExplainsThatGtkIsNotAScreenCastProvider()
    {
        var environment = CreateEnvironment("Sway");
        var config = "/config/xdg-desktop-portal/sway-portals.conf";

        var result = PortalScreenCastProviderDiagnostic.Describe(
            environment,
            path => string.Equals(path, config, StringComparison.Ordinal),
            path => string.Equals(path, config, StringComparison.Ordinal)
                ? "[preferred]\norg.freedesktop.impl.portal.ScreenCast=gtk"
                : null);

        Assert.Contains("does not implement ScreenCast", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compositor's ScreenCast backend", result, StringComparison.OrdinalIgnoreCase);
    }

    private static LinuxEnvironmentSnapshot CreateEnvironment(string desktop) => new(
        FlatpakId: "io.github.alper_han.crossmacro",
        AppImage: null,
        UseDaemon: null,
        SessionType: "wayland",
        WaylandDisplay: "wayland-1",
        Display: null,
        CurrentDesktop: desktop,
        GdmSession: desktop,
        HyprlandInstanceSignature: string.Equals(desktop, "Hyprland", StringComparison.OrdinalIgnoreCase) ? "instance" : null,
        RuntimeDir: "/run/user/1000",
        WayfireSocket: null,
        SwaySocket: null,
        WindowButtons: null,
        CrossMacroFlatpak: null,
        FlatpakInfoExists: true,
        NiriSocket: null,
        XdgConfigHome: "/config");
}
