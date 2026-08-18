namespace CrossMacro.Platform.Linux.Services;

public sealed class LinuxEnvironmentVariables : ILinuxEnvironmentVariables
{
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly Func<string, bool> _fileExists;
    private readonly LinuxEnvironmentSnapshot? _fixedSnapshot;

    public static LinuxEnvironmentSnapshot CaptureCurrentSnapshot()
    {
        return new LinuxEnvironmentVariables(Environment.GetEnvironmentVariable, File.Exists).CaptureSnapshot();
    }

    /// <summary>
    /// Captures the live environment at call time. Prefer the snapshot-backed
    /// constructor in production composition so the environment is captured
    /// once at the boundary and passed through.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public LinuxEnvironmentVariables()
        : this(Environment.GetEnvironmentVariable, File.Exists) { /* Empty */ }

    public LinuxEnvironmentVariables(LinuxEnvironmentSnapshot snapshot)
    {
        _fixedSnapshot = snapshot;
        _getEnvironmentVariable = static _ => null;
        _fileExists = static _ => false;
    }

    internal LinuxEnvironmentVariables(Func<string, string?> getEnvironmentVariable)
        : this(getEnvironmentVariable, File.Exists) { /* Empty */ }

    internal LinuxEnvironmentVariables(Func<string, string?> getEnvironmentVariable, Func<string, bool> fileExists)
    {
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
    }

    public LinuxEnvironmentSnapshot CaptureSnapshot()
    {
        if (_fixedSnapshot is { } snapshot)
        {
            return snapshot;
        }

        return new LinuxEnvironmentSnapshot(
            FlatpakId: _getEnvironmentVariable("FLATPAK_ID"),
            AppImage: _getEnvironmentVariable("APPIMAGE"),
            SessionType: _getEnvironmentVariable("XDG_SESSION_TYPE"),
            WaylandDisplay: _getEnvironmentVariable("WAYLAND_DISPLAY"),
            Display: _getEnvironmentVariable("DISPLAY"),
            CurrentDesktop: _getEnvironmentVariable("XDG_CURRENT_DESKTOP"),
            GdmSession: _getEnvironmentVariable("GDMSESSION"),
            HyprlandInstanceSignature: _getEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE"),
            RuntimeDir: _getEnvironmentVariable("XDG_RUNTIME_DIR"),
            WayfireSocket: _getEnvironmentVariable("WAYFIRE_SOCKET"),
            SwaySocket: _getEnvironmentVariable("SWAYSOCK"),
            WindowButtons: _getEnvironmentVariable("CROSSMACRO_WINDOW_BUTTONS"),
            CrossMacroFlatpak: _getEnvironmentVariable("CROSSMACRO_FLATPAK"),
            FlatpakInfoExists: _fileExists("/.flatpak-info"),
            NiriSocket: _getEnvironmentVariable("NIRI_SOCKET"),
            XdgConfigHome: _getEnvironmentVariable("XDG_CONFIG_HOME"));
    }
}
