namespace CrossMacro.Platform.Linux.Services;

public sealed class LinuxEnvironmentVariables : ILinuxEnvironmentVariables
{
    private readonly Func<string, string?> _getEnvironmentVariable;

    public LinuxEnvironmentVariables()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    internal LinuxEnvironmentVariables(Func<string, string?> getEnvironmentVariable)
    {
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
    }

    public LinuxEnvironmentSnapshot CaptureSnapshot()
    {
        return new LinuxEnvironmentSnapshot(
            FlatpakId: _getEnvironmentVariable("FLATPAK_ID"),
            AppImage: _getEnvironmentVariable("APPIMAGE"),
            UseDaemon: _getEnvironmentVariable("CROSSMACRO_USE_DAEMON"),
            SessionType: _getEnvironmentVariable("XDG_SESSION_TYPE"),
            WaylandDisplay: _getEnvironmentVariable("WAYLAND_DISPLAY"),
            Display: _getEnvironmentVariable("DISPLAY"),
            CurrentDesktop: _getEnvironmentVariable("XDG_CURRENT_DESKTOP"),
            GdmSession: _getEnvironmentVariable("GDMSESSION"),
            HyprlandInstanceSignature: _getEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE"),
            RuntimeDir: _getEnvironmentVariable("XDG_RUNTIME_DIR"),
            WayfireSocket: _getEnvironmentVariable("WAYFIRE_SOCKET"),
            WindowButtons: _getEnvironmentVariable("CROSSMACRO_WINDOW_BUTTONS"));
    }
}
