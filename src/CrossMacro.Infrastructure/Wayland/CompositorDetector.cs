using System;
using Serilog;

namespace CrossMacro.Infrastructure.Wayland
{
    /// <summary>
    /// Detects the currently running Wayland compositor
    /// </summary>
    public static class CompositorDetector
    {
        /// <summary>
        /// Detects the current compositor by checking environment variables
        /// </summary>
        public static CompositorType DetectCompositor()
        {
            // Check session type (X11 vs Wayland) - for future use
            var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
            var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
            
            // Currently we only support Wayland, but this check is here for future X11 filtering
            var isWayland = !string.IsNullOrEmpty(waylandDisplay) || 
                           string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase);
            
            if (!isWayland)
            {
                Log.Warning("[CompositorDetector] X11 session detected - not supported");
                return CompositorType.Unknown;
            }

            // Detect specific compositor
            var currentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";
            var swaySock = Environment.GetEnvironmentVariable("SWAYSOCK");

            return currentDesktop.ToUpperInvariant() switch
            {
                var desktop when desktop.Contains("HYPRLAND") => 
                    LogAndReturn(CompositorType.HYPRLAND, "Hyprland"),
                
                "KDE" => 
                    LogAndReturn(CompositorType.KDE, "KDE Plasma"),
                
                var desktop when desktop.Contains("GNOME") => 
                    LogAndReturn(CompositorType.GNOME, "GNOME"),
                
                var desktop when desktop.Contains("SWAY") || !string.IsNullOrEmpty(swaySock) => 
                    LogAndReturn(CompositorType.SWAY, "Sway"),
                
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
