using System.Security.Cryptography;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal static class PortalScreenCastRestoreContext
{
    private const string Version = "v1";

    public static string Create(LinuxEnvironmentSnapshot environment)
    {
        var compositor = CompositorDetector.ClassifyFromEnvironment(environment, isLinux: true);
        var canonical = string.Join(
            '\n',
            Version,
            compositor,
            Normalize(environment.FlatpakId),
            Normalize(environment.SessionType),
            Normalize(environment.WaylandDisplay),
            Normalize(environment.CurrentDesktop),
            Normalize(environment.GdmSession));

        return $"crossmacro-screen-cast-{Version}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))}";
    }

    private static string Normalize(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;
}
