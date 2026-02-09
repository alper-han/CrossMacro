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

            // Flatpak with hybrid mode support
            // Check if daemon is available for secure Wayland operation
            var useDaemon = Environment.GetEnvironmentVariable("CROSSMACRO_USE_DAEMON");
            bool hasDaemon = useDaemon == "1";

            var compositor = CompositorDetector.DetectCompositor();
            var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
            bool isWaylandSession = string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase);

            // X11 session - always supported
            if (compositor == CompositorType.X11 && !isWaylandSession)
            {
                Log.Information("[LinuxDisplaySessionService] Flatpak running on X11. Supported.");
                return true;
            }

            // Wayland session with daemon - supported (hybrid secure mode)
            if (isWaylandSession && hasDaemon)
            {
                Log.Information("[LinuxDisplaySessionService] Flatpak on Wayland with daemon. Supported (hybrid secure mode).");
                return true;
            }

            // Wayland session without daemon - supported with device permissions
            if (isWaylandSession)
            {
                Log.Information("[LinuxDisplaySessionService] Flatpak on Wayland without daemon. Using direct device access.");
                return true;
            }

            // Any other compositor - allow with warning
            Log.Information("[LinuxDisplaySessionService] Flatpak on {Compositor}. Allowing.", compositor);
            return true;
        }
    }
}
