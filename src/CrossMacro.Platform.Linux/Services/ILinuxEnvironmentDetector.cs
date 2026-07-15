
namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// Detects the current Linux display server environment.
/// </summary>
public interface ILinuxEnvironmentDetector
{
    /// <summary>
    /// Gets the detected compositor type.
    /// Result is cached after first detection.
    /// </summary>
    public CompositorType DetectedCompositor { get; }

    /// <summary>
    /// Determines if the current session is Wayland-based.
    /// </summary>
    public bool IsWayland { get; }

    /// <summary>
    /// Determines if the current session is X11-based.
    /// </summary>
    public bool IsX11 { get; }
}
