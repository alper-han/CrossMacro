
namespace CrossMacro.Platform.Linux.Services;

public sealed class LinuxRuntimeContext(LinuxEnvironmentSnapshot environment) : IRuntimeContext, IDisplayEnvironmentDiagnostic
{
    private readonly LinuxEnvironmentSnapshot _environment = environment;

    public bool IsLinux => true;
    public bool IsWindows => false;
    public bool IsMacOS => false;
    public bool IsFlatpak => _environment.IsFlatpak;
    public string? SessionType => _environment.SessionType;
    public string? Display => _environment.Display;
    public string? WaylandDisplay => _environment.WaylandDisplay;
    public string? XdgSessionType => _environment.SessionType;
}
