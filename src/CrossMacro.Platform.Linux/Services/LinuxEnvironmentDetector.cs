using CrossMacro.Platform.Linux.DisplayServer;

namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// Detects the current Linux display server environment.
/// Wraps the static CompositorDetector for dependency injection and testability.
/// </summary>
public class LinuxEnvironmentDetector : ILinuxEnvironmentDetector
{
    private readonly Lazy<CompositorType> _compositor;
    
    public LinuxEnvironmentDetector()
    {
        _compositor = new Lazy<CompositorType>(CompositorDetector.DetectCompositor);
    }
    
    public CompositorType DetectedCompositor => _compositor.Value;
    
    public bool IsWayland => DetectedCompositor switch
    {
        CompositorType.HYPRLAND => true,
        CompositorType.WAYFIRE => true,
        CompositorType.GNOME => true,
        CompositorType.KDE => true,
        CompositorType.Other => true,
        _ => false
    };
    
    public bool IsX11 => DetectedCompositor == CompositorType.X11;
}
