
namespace CrossMacro.UI.ViewModels;

internal sealed class DesignRuntimeContext : IRuntimeContext
{
    public bool IsLinux => true;

    public bool IsWindows => false;

    public bool IsMacOS => false;

    public bool IsFlatpak => false;

    public string? SessionType => "x11";
}
