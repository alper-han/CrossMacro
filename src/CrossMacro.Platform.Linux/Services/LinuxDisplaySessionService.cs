using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Ipc;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.Ipc;
using Serilog;

namespace CrossMacro.Platform.Linux.Services
{
    public class LinuxDisplaySessionService : IDisplaySessionService
    {
        private static readonly TimeSpan DaemonHandshakeProbeTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan DaemonHandshakeStartupBudget = TimeSpan.FromSeconds(5);

        private readonly Func<string, bool> _fileExists;
        private readonly Func<string, bool> _canOpenForWrite;
        private readonly Func<string, bool> _canOpenForRead;
        private readonly Func<string, bool> _daemonHandshakeProbe;
        private readonly Func<string[]> _getInputEventCandidates;

        public LinuxDisplaySessionService()
            : this(
                File.Exists,
                CanOpenForWrite,
                CanOpenForRead,
                ProbeDaemonHandshake,
                GetInputEventCandidates)
        {
        }

        public LinuxDisplaySessionService(
            Func<string, bool> fileExists,
            Func<string, bool> canOpenForWrite)
            : this(
                fileExists,
                canOpenForWrite,
                CanOpenForRead,
                ProbeDaemonHandshake,
                GetInputEventCandidates)
        {
        }

        public LinuxDisplaySessionService(
            Func<string, bool> fileExists,
            Func<string, bool> canOpenForWrite,
            Func<string, bool> canOpenForRead,
            Func<string, bool> daemonHandshakeProbe,
            Func<string[]> getInputEventCandidates)
        {
            _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
            _canOpenForWrite = canOpenForWrite ?? throw new ArgumentNullException(nameof(canOpenForWrite));
            _canOpenForRead = canOpenForRead ?? throw new ArgumentNullException(nameof(canOpenForRead));
            _daemonHandshakeProbe = daemonHandshakeProbe ?? throw new ArgumentNullException(nameof(daemonHandshakeProbe));
            _getInputEventCandidates = getInputEventCandidates ?? throw new ArgumentNullException(nameof(getInputEventCandidates));
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
                if (HasDaemonHandshakeAccess())
                {
                    Log.Information("[LinuxDisplaySessionService] Flatpak on Wayland with daemon handshake access. Supported (hybrid secure mode).");
                    return true;
                }

                if (HasDirectInputAccess())
                {
                    Log.Warning("[LinuxDisplaySessionService] Daemon handshake failed, but direct input fallback is ready. Continuing in direct mode.");
                    return true;
                }

                reason = "Daemon handshake failed and direct fallback is not ready (/dev/uinput write + readable /dev/input/event* required).";
                Log.Warning("[LinuxDisplaySessionService] {Reason}", reason);
                return false;
            }

            // Wayland direct mode fallback requires /dev/uinput write + readable /dev/input/event*.
            if (HasDirectInputAccess())
            {
                Log.Information("[LinuxDisplaySessionService] Flatpak on Wayland without daemon. Using direct device access.");
                return true;
            }

            reason = "Wayland direct mode requires /dev/uinput write access and readable /dev/input/event* devices.";
            Log.Warning("[LinuxDisplaySessionService] {Reason}", reason);
            return false;
        }

        private bool HasDaemonHandshakeAccess()
        {
            var socketPath = ResolveAvailableSocketPath();
            if (socketPath == null)
            {
                return false;
            }

            try
            {
                var probeTask = Task.Run(() => _daemonHandshakeProbe(socketPath));
                if (probeTask.Wait(DaemonHandshakeStartupBudget))
                {
                    return probeTask.Result;
                }

                Log.Warning(
                    "[LinuxDisplaySessionService] Daemon handshake probe exceeded startup budget ({BudgetMs}ms). Continuing without blocking UI thread.",
                    DaemonHandshakeStartupBudget.TotalMilliseconds);
                return false;
            }
            catch
            {
                return false;
            }
        }

        private string? ResolveAvailableSocketPath()
        {
            if (_fileExists(IpcProtocol.DefaultSocketPath))
            {
                return IpcProtocol.DefaultSocketPath;
            }

            if (_fileExists(IpcProtocol.FallbackSocketPath))
            {
                return IpcProtocol.FallbackSocketPath;
            }

            return null;
        }

        private bool HasDirectInputAccess()
        {
            return HasUInputWriteAccess() && HasReadableInputEventAccess();
        }

        private bool HasUInputWriteAccess()
        {
            return _canOpenForWrite(LinuxConstants.UInputDevicePath) ||
                   _canOpenForWrite(LinuxConstants.UInputAlternatePath);
        }

        private bool HasReadableInputEventAccess()
        {
            try
            {
                var eventDevices = _getInputEventCandidates();
                if (eventDevices.Length == 0)
                {
                    return false;
                }

                return eventDevices.Any(_canOpenForRead);
            }
            catch
            {
                return false;
            }
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

        private static bool CanOpenForRead(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string[] GetInputEventCandidates()
        {
            try
            {
                if (!Directory.Exists("/dev/input"))
                {
                    return [];
                }

                return Directory.GetFiles("/dev/input", "event*");
            }
            catch
            {
                return [];
            }
        }

        private static bool ProbeDaemonHandshake(string socketPath)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(DaemonHandshakeProbeTimeout);
                using var client = new IpcClient(() => socketPath, autoReconnect: false);
                client.ConnectAsync(timeoutCts.Token).GetAwaiter().GetResult();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
