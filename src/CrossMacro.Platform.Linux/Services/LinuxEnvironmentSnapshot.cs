namespace CrossMacro.Platform.Linux.Services;

public readonly record struct LinuxEnvironmentSnapshot(
    string? FlatpakId,
    string? AppImage,
    string? UseDaemon,
    string? SessionType,
    string? WaylandDisplay,
    string? Display,
    string? CurrentDesktop,
    string? GdmSession,
    string? HyprlandInstanceSignature,
    string? RuntimeDir,
    string? WayfireSocket,
    string? SwaySocket,
    string? WindowButtons,
    string? CrossMacroFlatpak = null,
    bool FlatpakInfoExists = false,
    string? NiriSocket = null,
    string? XdgConfigHome = null)
{
    public bool IsFlatpak =>
        !string.IsNullOrWhiteSpace(FlatpakId) ||
        string.Equals(CrossMacroFlatpak, "1", StringComparison.Ordinal) ||
        FlatpakInfoExists;
}
