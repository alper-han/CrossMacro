
namespace CrossMacro.Platform.Linux.DisplayServer;

/// <summary>
/// Detects the currently running display server / compositor
/// </summary>
public static class CompositorDetector
{
    private static readonly Lazy<CompositorType> _current = new(static () =>
    {
        var environment = LinuxEnvironmentVariables.CaptureCurrentSnapshot();
        var compositor = ClassifyFromEnvironment(environment, OperatingSystem.IsLinux());
        Log.Information(
            "[CompositorDetector] Detected {Compositor} (SessionType: {SessionType}, WaylandDisplay: {WaylandDisplay}, Display: {Display}, Desktop: {Desktop})",
            compositor,
            environment.SessionType ?? "null",
            environment.WaylandDisplay ?? "null",
            environment.Display ?? "null",
            environment.CurrentDesktop ?? "null");
        return compositor;
    });

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

        var session = LinuxDisplaySessionClassifier.Classify(environment);
        if (session is LinuxSessionType.X11)
        {
            return CompositorType.X11;
        }

        return session is LinuxSessionType.Wayland ? ClassifyWaylandCompositor(environment) : CompositorType.Unknown;
    }

    private static CompositorType ClassifyWaylandCompositor(LinuxEnvironmentSnapshot environment)
    {
        if (LinuxDisplaySessionClassifier.HasDesktop(environment, "HYPRLAND"))
        {
            return CompositorType.HYPRLAND;
        }

        if (LinuxDisplaySessionClassifier.HasDesktop(environment, "WAYFIRE") || !string.IsNullOrWhiteSpace(environment.WayfireSocket))
        {
            return CompositorType.WAYFIRE;
        }

        if (LinuxDisplaySessionClassifier.HasDesktop(environment, "NIRI") || LinuxDisplaySessionClassifier.GdmSessionContains(environment, "NIRI"))
        {
            return CompositorType.NIRI;
        }

        if (LinuxDisplaySessionClassifier.HasDesktop(environment, "COSMIC") || LinuxDisplaySessionClassifier.GdmSessionContains(environment, "COSMIC"))
        {
            return CompositorType.COSMIC;
        }

        if (LinuxDisplaySessionClassifier.HasDesktop(environment, "SWAY") || !string.IsNullOrWhiteSpace(environment.SwaySocket))
        {
            return CompositorType.SWAY;
        }

        if (LinuxDisplaySessionClassifier.IsKde(environment))
        {
            return CompositorType.KDE;
        }

        return LinuxDisplaySessionClassifier.IsGnome(environment) ? CompositorType.GNOME : CompositorType.Other;
    }
}
