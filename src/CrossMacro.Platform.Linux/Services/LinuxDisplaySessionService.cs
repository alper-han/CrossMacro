using System;
using System.IO;
using CrossMacro.Core.Ipc;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.DisplayServer;
using Serilog;

namespace CrossMacro.Platform.Linux.Services
{
    public class LinuxDisplaySessionService : IDisplaySessionService
    {
        private readonly Func<string, bool> _fileExists;
        private readonly Func<string, bool> _canOpenForWrite;

        public LinuxDisplaySessionService()
            : this(File.Exists, CanOpenForWrite)
        {
        }

        public LinuxDisplaySessionService(
            Func<string, bool> fileExists,
            Func<string, bool> canOpenForWrite)
        {
            _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
            _canOpenForWrite = canOpenForWrite ?? throw new ArgumentNullException(nameof(canOpenForWrite));
        }

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

            var useDaemon = Environment.GetEnvironmentVariable("CROSSMACRO_USE_DAEMON");
            bool hasDaemon = useDaemon == "1";

            var compositor = CompositorDetector.DetectCompositor();
            var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
            bool isWaylandSession = string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase);
            bool isX11Session = string.Equals(sessionType, "x11", StringComparison.OrdinalIgnoreCase);

            // X11 session - always supported
            if (compositor == CompositorType.X11 || isX11Session)
            {
                Log.Information("[LinuxDisplaySessionService] Flatpak running on X11. Supported.");
                return true;
            }

            if (!isWaylandSession)
            {
                reason = "Unsupported Flatpak session. CrossMacro requires an X11 or Wayland desktop session.";
                Log.Warning("[LinuxDisplaySessionService] {Reason} SessionType={SessionType}, Compositor={Compositor}",
                    reason, sessionType ?? "unknown", compositor);
                return false;
            }

            // Wayland + daemon mode requires the daemon socket to be mounted into the sandbox.
            if (hasDaemon)
            {
                if (_fileExists(IpcProtocol.DefaultSocketPath) || _fileExists(IpcProtocol.FallbackSocketPath))
                {
                    Log.Information("[LinuxDisplaySessionService] Flatpak on Wayland with daemon socket. Supported (hybrid secure mode).");
                    return true;
                }

                reason = "Daemon mode is enabled but the CrossMacro daemon socket is not accessible inside Flatpak.";
                Log.Warning("[LinuxDisplaySessionService] {Reason}", reason);
                return false;
            }

            // Wayland direct mode fallback requires write access to /dev/uinput.
            if (HasUInputWriteAccess())
            {
                Log.Information("[LinuxDisplaySessionService] Flatpak on Wayland without daemon. Using direct device access.");
                return true;
            }

            reason = "Wayland direct mode requires /dev/uinput write access. Start the daemon or grant device access to Flatpak.";
            Log.Warning("[LinuxDisplaySessionService] {Reason}", reason);
            return false;
        }

        private bool HasUInputWriteAccess()
        {
            return _canOpenForWrite(LinuxConstants.UInputDevicePath) ||
                   _canOpenForWrite(LinuxConstants.UInputAlternatePath);
        }

        private static bool CanOpenForWrite(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                using var fs = File.OpenWrite(path);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
