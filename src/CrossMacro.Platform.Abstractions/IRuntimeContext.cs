namespace CrossMacro.Platform.Abstractions;

public interface IRuntimeContext
{
    public bool IsLinux { get; }
    public bool IsWindows { get; }
    public bool IsMacOS { get; }
    public bool IsFlatpak { get; }
    public string? SessionType { get; }
}
