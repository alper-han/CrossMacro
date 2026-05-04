using System;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Linux.Services;

namespace CrossMacro.Platform.Linux.DisplayServer
{
    /// <summary>
    /// Detects the currently running display server / compositor
    /// </summary>
    public static class CompositorDetector
    {
        /// <summary>
        /// Detects the current compositor by checking environment variables
        /// </summary>
        private static readonly Lazy<CompositorType> _current = new(() =>
            ClassifyFromEnvironment(
                new LinuxEnvironmentVariables().CaptureSnapshot(),
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

            Log.Information("[CompositorDetector] Environment Detection - SessionType: {SessionType}, WaylandDisplay: {WaylandDisplay}, Display: {Display}",
                environment.SessionType ?? "null", environment.WaylandDisplay ?? "null", environment.Display ?? "null");

            var isWayland = !string.IsNullOrEmpty(environment.WaylandDisplay) ||
                            string.Equals(environment.SessionType, "wayland", StringComparison.OrdinalIgnoreCase);

            var isX11 = !string.IsNullOrEmpty(environment.Display) ||
                        string.Equals(environment.SessionType, "x11", StringComparison.OrdinalIgnoreCase);

            Log.Information("[CompositorDetector] Session Flags - IsWayland: {IsWayland}, IsX11: {IsX11}", isWayland, isX11);

            if (isX11 && !isWayland)
            {
                Log.Information("[CompositorDetector] X11 session detected");
                return CompositorType.X11;
            }

            if (!isWayland)
            {
                Log.Warning("[CompositorDetector] No known display server detected");
                return CompositorType.Unknown;
            }

            var currentDesktop = environment.CurrentDesktop ?? "";
            var gdmSession = environment.GdmSession ?? "";
            var desktopIdentity = $"{currentDesktop}:{gdmSession}".ToUpperInvariant();

            return currentDesktop.ToUpperInvariant() switch
            {
                var desktop when desktop.Contains("HYPRLAND") =>
                    LogAndReturn(CompositorType.HYPRLAND, "Hyprland"),

                var desktop when desktop.Contains("WAYFIRE") || !string.IsNullOrWhiteSpace(environment.WayfireSocket) =>
                    LogAndReturn(CompositorType.WAYFIRE, "Wayfire"),

                _ when desktopIdentity.Contains("NIRI") =>
                    LogAndReturn(CompositorType.NIRI, "Niri"),

                _ when desktopIdentity.Contains("COSMIC") =>
                    LogAndReturn(CompositorType.COSMIC, "COSMIC"),

                "KDE" =>
                    LogAndReturn(CompositorType.KDE, "KDE Plasma"),

                var desktop when desktop.Contains("GNOME") =>
                    LogAndReturn(CompositorType.GNOME, "GNOME"),

                _ when isWayland =>
                    LogAndReturnUnknown(currentDesktop),

                _ => CompositorType.Unknown
            };
        }

        private static CompositorType LogAndReturn(CompositorType type, string name)
        {
            Log.Information("[CompositorDetector] Detected {Compositor}", name);
            return type;
        }

        private static CompositorType LogAndReturnUnknown(string desktop)
        {
            Log.Information("[CompositorDetector] Wayland session detected but specific compositor unknown (Desktop: {Desktop})", desktop);
            return CompositorType.Other;
        }
    }
}
