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
    string? WindowButtons);
