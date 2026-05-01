namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Provides platform-agnostic environment information for UI decisions.
/// Implementations are platform-specific and live in outer layers.
/// </summary>
public interface IEnvironmentInfoProvider
{
    /// <summary>
    /// Gets the detected display environment.
    /// </summary>
    DisplayEnvironment CurrentEnvironment { get; }

    /// <summary>
    /// Whether the window manager handles its own close button.
    /// True for tiling WMs like Hyprland, i3, sway where custom title bars are not needed.
    /// </summary>
    bool WindowManagerHandlesCloseButton { get; }
}
