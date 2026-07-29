
namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// Detects the current Linux display server environment.
/// Wraps the static CompositorDetector for dependency injection and testability.
/// </summary>
public class LinuxEnvironmentDetector : ILinuxEnvironmentDetector
{
    private readonly Lazy<CompositorType> _compositor;

    /// <summary>
    /// Captures the live environment at call time. Prefer the snapshot-backed
    /// constructor in production composition so the environment is captured
    /// once at the boundary and passed through.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    internal LinuxEnvironmentDetector()
        : this(new LinuxEnvironmentVariables(LinuxEnvironmentVariables.CaptureCurrentSnapshot())) { /* Empty */ }

    public LinuxEnvironmentDetector(ILinuxEnvironmentVariables environmentVariables)
    {
        ArgumentNullException.ThrowIfNull(environmentVariables);

        _compositor = new Lazy<CompositorType>(() => CompositorDetector.ClassifyFromEnvironment(
            environmentVariables.CaptureSnapshot(),
            OperatingSystem.IsLinux()));
    }

    public CompositorType DetectedCompositor => _compositor.Value;

    public bool IsWayland => DetectedCompositor switch
    {
        CompositorType.HYPRLAND => true,
        CompositorType.WAYFIRE => true,
        CompositorType.NIRI => true,
        CompositorType.COSMIC => true,
        CompositorType.SWAY => true,
        CompositorType.GNOME => true,
        CompositorType.KDE => true,
        CompositorType.Other => true,
        CompositorType.Unknown => false,
        CompositorType.X11 => false,
        _ => false,
    };

    public bool IsX11 => DetectedCompositor is CompositorType.X11;
}
