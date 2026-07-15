using CrossMacro.Platform.Linux.DisplayServer;

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
    CompositorType DetectedCompositor { get; }

    /// <summary>
    /// Determines if the current session is Wayland-based.
    /// </summary>
    bool IsWayland { get; }

    /// <summary>
    /// Determines if the current session is X11-based.
    /// </summary>
    bool IsX11 { get; }
}
