
using CrossMacro.Platform.Linux.Extensions;

namespace CrossMacro.Platform.Linux.DisplayServer;

/// <summary>
/// Detects the currently running display server / compositor
/// </summary>
public static class CompositorDetector
{
    /// <summary>
    /// Detects the current compositor by checking environment variables
    /// </summary>
    private static readonly Lazy<CompositorType> _current = new(static () =>
        ClassifyFromEnvironment(
            LinuxEnvironmentVariables.CaptureCurrentSnapshot(),
            OperatingSystem.IsLinux()));

    /// <summary>
    /// Detects the current compositor by checking environment variables
    /// </summary>
    public static CompositorType DetectCompositor() => _current.Value;

    internal static CompositorType ClassifyFromEnvironment(LinuxEnvironmentSnapshot environment, bool isLinux = true)
    {
        if (!isLinux)
        {
            return CompositorType.Unknown;
        }

        var environmentKey = $"{environment.SessionType}|{environment.WaylandDisplay}|{environment.Display}";
        LoggingExtensions.LogOnce($"CompositorDetector.Environment.{environmentKey}", "[CompositorDetector] Environment Detection - SessionType: {SessionType}, WaylandDisplay: {WaylandDisplay}, Display: {Display}",
            environment.SessionType ?? "null", environment.WaylandDisplay ?? "null", environment.Display ?? "null");

        var isWayland = !string.IsNullOrEmpty(environment.WaylandDisplay) ||
                        string.Equals(environment.SessionType, "wayland", StringComparison.OrdinalIgnoreCase);

        var isX11 = !string.IsNullOrEmpty(environment.Display) ||
                    string.Equals(environment.SessionType, "x11", StringComparison.OrdinalIgnoreCase);

        LoggingExtensions.LogOnce($"CompositorDetector.Flags.{environmentKey}", "[CompositorDetector] Session Flags - IsWayland: {IsWayland}, IsX11: {IsX11}", isWayland, isX11);

        if (isX11 && !isWayland)
        {
            LoggingExtensions.LogOnce("CompositorDetector.X11", "[CompositorDetector] X11 session detected");
            return CompositorType.X11;
        }

        if (!isWayland)
        {
            LoggingExtensions.LogOnce("CompositorDetector.UnknownDisplay", "[CompositorDetector] No known display server detected");
            return CompositorType.Unknown;
        }

        return ClassifyWaylandCompositor(environment, isWayland);
    }

    private static CompositorType ClassifyWaylandCompositor(LinuxEnvironmentSnapshot environment, bool isWayland)
    {
        var currentDesktop = environment.CurrentDesktop ?? "";
        var gdmSession = environment.GdmSession ?? "";
        var desktopIdentity = $"{currentDesktop}:{gdmSession}".ToUpperInvariant();

        return currentDesktop.ToUpperInvariant() switch
        {
            var desktop when desktop.Contains("HYPRLAND", StringComparison.Ordinal) =>
                LogAndReturn(CompositorType.HYPRLAND, "Hyprland"),

            var desktop when desktop.Contains("WAYFIRE", StringComparison.Ordinal) || !string.IsNullOrWhiteSpace(environment.WayfireSocket) =>
                LogAndReturn(CompositorType.WAYFIRE, "Wayfire"),

            _ when desktopIdentity.Contains("NIRI", StringComparison.Ordinal) =>
                LogAndReturn(CompositorType.NIRI, "Niri"),

            _ when desktopIdentity.Contains("COSMIC", StringComparison.Ordinal) =>
                LogAndReturn(CompositorType.COSMIC, "COSMIC"),

            var desktop when desktop.Contains("SWAY", StringComparison.Ordinal) || !string.IsNullOrWhiteSpace(environment.SwaySocket) =>
                LogAndReturn(CompositorType.SWAY, "Sway"),

            "KDE" =>
                LogAndReturn(CompositorType.KDE, "KDE Plasma"),

            var desktop when desktop.Contains("GNOME", StringComparison.Ordinal) =>
                LogAndReturn(CompositorType.GNOME, "GNOME"),

            _ when isWayland =>
                LogAndReturnUnknown(currentDesktop),

            _ => CompositorType.Unknown,
        };
    }

    private static CompositorType LogAndReturn(CompositorType type, string name)
    {
        LoggingExtensions.LogOnce($"CompositorDetector.Detected.{type}", "[CompositorDetector] Detected {Compositor}", name);
        return type;
    }

    private static CompositorType LogAndReturnUnknown(string desktop)
    {
        LoggingExtensions.LogOnce($"CompositorDetector.UnknownWayland.{desktop}", "[CompositorDetector] Wayland session detected but specific compositor unknown (Desktop: {Desktop})", desktop);
        return CompositorType.Other;
    }
}
