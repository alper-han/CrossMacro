
namespace CrossMacro.Platform.Linux.Services.Runtime;

/// <summary>
/// Linux-specific implementation of IEnvironmentInfoProvider.
/// Wraps CompositorDetector for cross-platform abstraction.
/// </summary>
public class LinuxEnvironmentInfoProvider : IEnvironmentInfoProvider
{
    private const string WindowButtonsEnvKey = "CROSSMACRO_WINDOW_BUTTONS";
    private readonly CompositorType _compositor;
    private readonly bool _windowManagerHandlesCloseButton;

    /// <summary>
    /// Captures the live environment at call time. Prefer the snapshot-backed
    /// constructor in production composition so the environment is captured
    /// once at the boundary and passed through.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    internal LinuxEnvironmentInfoProvider()
        : this(CompositorDetector.DetectCompositor(), LinuxEnvironmentVariables.CaptureCurrentSnapshot().WindowButtons) { /* Empty */ }

    public LinuxEnvironmentInfoProvider(
        ILinuxEnvironmentDetector environmentDetector,
        ILinuxEnvironmentVariables environmentVariables)
        : this(
            (environmentDetector ?? throw new ArgumentNullException(nameof(environmentDetector))).DetectedCompositor,
            (environmentVariables ?? throw new ArgumentNullException(nameof(environmentVariables))).CaptureSnapshot().WindowButtons)
    { /* Empty */ }

    public LinuxEnvironmentInfoProvider(LinuxEnvironmentSnapshot environment)
        : this(
            CompositorDetector.ClassifyFromEnvironment(environment, OperatingSystem.IsLinux()),
            environment.WindowButtons)
    { /* Empty */ }

    /// <summary>
    /// Constructor for testing with explicit compositor type.
    /// </summary>
    internal LinuxEnvironmentInfoProvider(CompositorType compositor)
        : this(compositor, (string?)null) { /* Empty */ }

    /// <summary>
    /// Constructor for testing with explicit compositor type and environment accessor.
    /// </summary>
    internal LinuxEnvironmentInfoProvider(
        CompositorType compositor,
        Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        Initialize(compositor, getEnvironmentVariable(WindowButtonsEnvKey), out _compositor, out _windowManagerHandlesCloseButton);
    }

    private LinuxEnvironmentInfoProvider(CompositorType compositor, string? windowButtonsMode)
    {
        Initialize(compositor, windowButtonsMode, out _compositor, out _windowManagerHandlesCloseButton);
    }

    private static void Initialize(
        CompositorType compositor,
        string? windowButtonsMode,
        out CompositorType resolvedCompositor,
        out bool windowManagerHandlesCloseButton)
    {
        resolvedCompositor = compositor;
        windowManagerHandlesCloseButton = ResolveWindowManagerHandlesCloseButton(
            compositor,
            windowButtonsMode);
    }

    public DisplayEnvironment CurrentEnvironment => _compositor switch
    {
        CompositorType.X11 => DisplayEnvironment.LinuxX11,
        CompositorType.Hyprland => DisplayEnvironment.LinuxHyprland,
        CompositorType.Wayfire => DisplayEnvironment.LinuxWayfire,
        CompositorType.Niri => DisplayEnvironment.LinuxWayland,
        CompositorType.Cosmic => DisplayEnvironment.LinuxWayland,
        CompositorType.Sway => DisplayEnvironment.LinuxWayland,
        CompositorType.Kde => DisplayEnvironment.LinuxKDE,
        CompositorType.Gnome => DisplayEnvironment.LinuxGnome,
        CompositorType.Other => DisplayEnvironment.LinuxWayland,
        CompositorType.Unknown => DisplayEnvironment.Unknown,
        _ => DisplayEnvironment.Unknown,
    };

    public bool WindowManagerHandlesCloseButton => _windowManagerHandlesCloseButton;

    private static bool ResolveWindowManagerHandlesCloseButton(
        CompositorType compositor,
        string? windowButtonsMode)
    {
        // Default behavior: on tiling WMs like Hyprland, Sway, and Niri, let compositor title bar controls own close/minimize affordance.
        var defaultValue = compositor is CompositorType.Hyprland or CompositorType.Sway or CompositorType.Niri;

        if (string.IsNullOrWhiteSpace(windowButtonsMode))
        {
            return defaultValue;
        }

        return windowButtonsMode.Trim().ToUpperInvariant() switch
        {
            "SHOW" or "1" or "TRUE" or "YES" or "ON" => false,
            "HIDE" or "0" or "FALSE" or "NO" or "OFF" => true,
            "AUTO" => defaultValue,
            _ => defaultValue,
        };
    }
}
