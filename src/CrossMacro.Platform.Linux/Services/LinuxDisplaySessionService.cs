using System;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.DisplayServer;
using Serilog;

namespace CrossMacro.Platform.Linux.Services
{
    public class LinuxDisplaySessionService : IDisplaySessionService
    {
        public bool IsSessionSupported(out string reason)
        {
            reason = string.Empty;

            // check if running in Flatpak
            var flatpakId = Environment.GetEnvironmentVariable("FLATPAK_ID");
            bool isFlatpak = !string.IsNullOrEmpty(flatpakId);

            Log.Information("[LinuxDisplaySessionService] Checking Session Support. Flatpak: {IsFlatpak}, ID: {FlatpakId}", 
                isFlatpak, flatpakId ?? "null");

            if (!isFlatpak)
            {
                return true;
            }

            // We are in Flatpak. Check for Wayland.
            var compositor = CompositorDetector.DetectCompositor();
            
            // Explicitly allow X11
            if (compositor == CompositorType.X11)
            {
                Log.Information("[LinuxDisplaySessionService] Flatpak running on X11. Supported.");
                return true;
            }

            var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
            bool isWaylandSession = string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase);

            if (isWaylandSession || 
                compositor == CompositorType.KDE || 
                compositor == CompositorType.GNOME || 
                compositor == CompositorType.HYPRLAND || 
                compositor == CompositorType.Other) 
            {
                 if (isWaylandSession)
                 {
                     reason = "CrossMacro Flatpak requires a native X11 session.\n\n" +
                              "Wayland is not supported due to sandbox restrictions which prevent global input automation.\n\n" +
                              "Please log out and select 'X11' or 'Xorg' session from your login screen.";
                     
                     Log.Error("[LinuxDisplaySessionService] Unsupported Session in Flatpak. Wayland detected. Blocking start.");
                     return false;
                 }
            }

            var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
            if (!string.IsNullOrEmpty(waylandDisplay))
            {
                 reason = "CrossMacro Flatpak requires a native X11 session.\n\n" +
                          "Wayland is not supported due to sandbox restrictions.\n\n" +
                          "Please use an X11 session.";
                 Log.Error("[LinuxDisplaySessionService] Wayland display detected in Flatpak. Blocking.");
                 return false;
            }

            return true;
        }
    }
}
