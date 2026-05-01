namespace CrossMacro.Platform.Abstractions;

public interface IRuntimeContext
{
    bool IsLinux { get; }
    bool IsWindows { get; }
    bool IsMacOS { get; }
    bool IsFlatpak { get; }
    string? SessionType { get; }
}
