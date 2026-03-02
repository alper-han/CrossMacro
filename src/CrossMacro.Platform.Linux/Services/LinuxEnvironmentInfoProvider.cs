using CrossMacro.Core.Services;
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
    
    public LinuxEnvironmentInfoProvider()
        : this(CompositorDetector.DetectCompositor(), Environment.GetEnvironmentVariable)
    {
    }
    
    /// <summary>
    /// Constructor for testing with explicit compositor type.
    /// </summary>
    internal LinuxEnvironmentInfoProvider(CompositorType compositor)
        : this(compositor, _ => null)
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

        _compositor = compositor;
        _windowManagerHandlesCloseButton = ResolveWindowManagerHandlesCloseButton(
            compositor,
            getEnvironmentVariable(WindowButtonsEnvKey));
    }
    
    public DisplayEnvironment CurrentEnvironment => _compositor switch
    {
        CompositorType.X11 => DisplayEnvironment.LinuxX11,
        CompositorType.HYPRLAND => DisplayEnvironment.LinuxHyprland,
        CompositorType.KDE => DisplayEnvironment.LinuxKDE,
        CompositorType.GNOME => DisplayEnvironment.LinuxGnome,
        CompositorType.Other => DisplayEnvironment.LinuxWayland,
        _ => DisplayEnvironment.Unknown
    };
    
    public bool WindowManagerHandlesCloseButton => _windowManagerHandlesCloseButton;

    private static bool ResolveWindowManagerHandlesCloseButton(
        CompositorType compositor,
        string? windowButtonsMode)
    {
        // Default behavior: on Hyprland, let compositor title bar controls own close/minimize affordance.
        var defaultValue = compositor == CompositorType.HYPRLAND;

        if (string.IsNullOrWhiteSpace(windowButtonsMode))
        {
            return defaultValue;
        }

        return windowButtonsMode.Trim().ToLowerInvariant() switch
        {
            "show" or "1" or "true" or "yes" or "on" => false,
            "hide" or "0" or "false" or "no" or "off" => true,
            "auto" => defaultValue,
            _ => defaultValue
        };
    }
}
