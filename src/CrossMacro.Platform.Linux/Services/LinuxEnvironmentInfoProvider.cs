using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer;

namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// Linux-specific implementation of IEnvironmentInfoProvider.
/// Wraps CompositorDetector for cross-platform abstraction.
/// </summary>
public class LinuxEnvironmentInfoProvider : IEnvironmentInfoProvider
{
    private const string WindowButtonsEnvKey = "CROSSMACRO_WINDOW_BUTTONS";
    private readonly CompositorType _compositor;
    private readonly bool _windowManagerHandlesCloseButton;

    [Obsolete("Use the snapshot-backed constructor in production composition.", error: false)]
    internal LinuxEnvironmentInfoProvider()
        : this(CompositorDetector.DetectCompositor(), LinuxEnvironmentVariables.CaptureCurrentSnapshot().WindowButtons)
    {
    }

    public LinuxEnvironmentInfoProvider(
        ILinuxEnvironmentDetector environmentDetector,
        ILinuxEnvironmentVariables environmentVariables)
        : this(
            (environmentDetector ?? throw new ArgumentNullException(nameof(environmentDetector))).DetectedCompositor,
            (environmentVariables ?? throw new ArgumentNullException(nameof(environmentVariables))).CaptureSnapshot().WindowButtons)
    {
    }

    public LinuxEnvironmentInfoProvider(LinuxEnvironmentSnapshot environment)
        : this(
            CompositorDetector.ClassifyFromEnvironment(environment, OperatingSystem.IsLinux()),
            environment.WindowButtons)
    {
    }

    /// <summary>
    /// Constructor for testing with explicit compositor type.
    /// </summary>
    internal LinuxEnvironmentInfoProvider(CompositorType compositor)
        : this(compositor, (string?)null)
    {
    }

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
        CompositorType.HYPRLAND => DisplayEnvironment.LinuxHyprland,
        CompositorType.WAYFIRE => DisplayEnvironment.LinuxWayfire,
        CompositorType.NIRI => DisplayEnvironment.LinuxWayland,
        CompositorType.COSMIC => DisplayEnvironment.LinuxWayland,
        CompositorType.SWAY => DisplayEnvironment.LinuxWayland,
        CompositorType.KDE => DisplayEnvironment.LinuxKDE,
        CompositorType.GNOME => DisplayEnvironment.LinuxGnome,
        CompositorType.Other => DisplayEnvironment.LinuxWayland,
        _ => DisplayEnvironment.Unknown,
    };

    public bool WindowManagerHandlesCloseButton => _windowManagerHandlesCloseButton;

    private static bool ResolveWindowManagerHandlesCloseButton(
        CompositorType compositor,
        string? windowButtonsMode)
    {
        // Default behavior: on tiling WMs like Hyprland, Sway, and Niri, let compositor title bar controls own close/minimize affordance.
        var defaultValue = compositor is CompositorType.HYPRLAND or CompositorType.SWAY or CompositorType.NIRI;

        if (string.IsNullOrWhiteSpace(windowButtonsMode))
        {
            return defaultValue;
        }

        return windowButtonsMode.Trim().ToLowerInvariant() switch
        {
            "show" or "1" or "true" or "yes" or "on" => false,
            "hide" or "0" or "false" or "no" or "off" => true,
            "auto" => defaultValue,
            _ => defaultValue,
        };
    }
}
