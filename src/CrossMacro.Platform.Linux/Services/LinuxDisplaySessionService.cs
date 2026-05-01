using System;
using System.IO;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Core.Logging;

namespace CrossMacro.Platform.Linux.Services
{
    public class LinuxDisplaySessionService : IDisplaySessionService
    {
        private static readonly TimeSpan DaemonHandshakeProbeTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan DaemonHandshakeStartupBudget = TimeSpan.FromSeconds(5);

        private readonly ILinuxInputCapabilitySnapshotProvider _snapshotProvider;
        private readonly ILinuxEnvironmentVariables _environmentVariables;

        public LinuxDisplaySessionService()
            : this(new LinuxInputCapabilitySnapshotProvider(), new LinuxEnvironmentVariables())
        {
        }

        internal LinuxDisplaySessionService(
            ILinuxInputCapabilitySnapshotProvider snapshotProvider,
            ILinuxEnvironmentVariables environmentVariables)
        {
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            _environmentVariables = environmentVariables ?? throw new ArgumentNullException(nameof(environmentVariables));
        }

        public LinuxDisplaySessionService(
            Func<string, bool> fileExists,
            Func<string, bool> canOpenForWrite)
            : this(
                new LinuxInputCapabilitySnapshotProvider(
                    fileExists,
                    canOpenForWrite,
                    LinuxInputProbeUtilities.CanOpenForRead,
                    LinuxInputCapabilityDetector.ProbeDaemonHandshakeWithinBudget,
                    LinuxInputProbeUtilities.GetInputEventCandidates),
                new LinuxEnvironmentVariables())
        {
        }

        public LinuxDisplaySessionService(
            Func<string, bool> fileExists,
            Func<string, bool> canOpenForWrite,
            Func<string, bool> canOpenForRead,
            Func<string, bool> daemonHandshakeProbe,
            Func<string[]> getInputEventCandidates)
            : this(
                new LinuxInputCapabilitySnapshotProvider(
                    fileExists,
                    canOpenForWrite,
                    canOpenForRead,
                    (socketPath, _) => daemonHandshakeProbe(socketPath)
                        ? LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Success()
                        : LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(),
                    getInputEventCandidates),
                new LinuxEnvironmentVariables())
        {
        }

        public LinuxDisplaySessionService(
            Func<string, bool> fileExists,
            Func<string, bool> canOpenForWrite,
            Func<string, bool> canOpenForRead,
            Func<string, TimeSpan, DaemonHandshakeProbeResult> daemonHandshakeProbe,
            Func<string[]> getInputEventCandidates)
            : this(
                new LinuxInputCapabilitySnapshotProvider(
                    fileExists,
                    canOpenForWrite,
                    canOpenForRead,
                    (socketPath, timeout) => MapDisplayProbeResult(daemonHandshakeProbe(socketPath, timeout)),
                    getInputEventCandidates),
                new LinuxEnvironmentVariables())
        {
        }

        public readonly record struct DaemonHandshakeProbeResult(bool Succeeded, bool TimedOut, Exception? Failure)
        {
            public static DaemonHandshakeProbeResult Success()
            {
                return new(true, false, null);
            }

            public static DaemonHandshakeProbeResult Failed(Exception? failure = null)
            {
                return new(false, false, failure);
            }

            public static DaemonHandshakeProbeResult Timeout(Exception? failure = null)
            {
                return new(false, true, failure);
            }
        }

        internal static DaemonHandshakeProbeResult ProbeDaemonHandshakeWithinBudget(string socketPath, TimeSpan timeout)
        {
            return MapProbeResult(LinuxDaemonHandshakeTransport.ProbeWithinBudget(socketPath, timeout));
        }

        private static LinuxInputCapabilityDetector.DaemonHandshakeProbeResult MapDisplayProbeResult(DaemonHandshakeProbeResult result)
        {
            return result.TimedOut
                ? LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Timeout(result.Failure)
                : result.Succeeded
                    ? LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Success()
                    : LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(result.Failure);
        }

        private static DaemonHandshakeProbeResult MapProbeResult(LinuxDaemonHandshakeTransport.ProbeResult result)
        {
            return result.TimedOut
                ? DaemonHandshakeProbeResult.Timeout(result.Failure)
                : result.Succeeded
                    ? DaemonHandshakeProbeResult.Success()
                    : DaemonHandshakeProbeResult.Failed(result.Failure);
        }

        public bool IsSessionSupported(out string reason)
        {
            reason = string.Empty;

            var environment = _environmentVariables.CaptureSnapshot();
            bool isFlatpak = !string.IsNullOrEmpty(environment.FlatpakId);

            Log.Information("[LinuxDisplaySessionService] Checking Session Support. Flatpak: {IsFlatpak}, ID: {FlatpakId}",
                isFlatpak, environment.FlatpakId ?? "null");

            if (!isFlatpak)
            {
                return true;
            }

            bool hasDaemon = environment.UseDaemon == "1";

            var compositor = CompositorDetector.ClassifyFromEnvironment(environment, OperatingSystem.IsLinux());
            bool isWaylandSession = string.Equals(environment.SessionType, "wayland", StringComparison.OrdinalIgnoreCase);
            bool isX11Session = string.Equals(environment.SessionType, "x11", StringComparison.OrdinalIgnoreCase);

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
                    reason, environment.SessionType ?? "unknown", compositor);
                return false;
            }

            LinuxInputCapabilitySnapshot? startupSnapshot = null;

            // Wayland + daemon mode requires the daemon socket to be mounted into the sandbox.
            if (hasDaemon)
            {
                if (HasDaemonHandshakeAccess(ref startupSnapshot))
                {
                    Log.Information("[LinuxDisplaySessionService] Flatpak on Wayland with daemon handshake access. Supported (hybrid secure mode).");
                    return true;
                }

                if (HasDirectInputAccess(ref startupSnapshot))
                {
                    Log.Warning("[LinuxDisplaySessionService] Daemon handshake failed, but direct input fallback is ready. Continuing in direct mode.");
                    return true;
                }

                reason = "Daemon handshake failed and direct fallback is not ready (/dev/uinput write + readable /dev/input/event* required).";
                Log.Warning("[LinuxDisplaySessionService] {Reason}", reason);
                return false;
            }

            // Wayland direct mode fallback requires /dev/uinput write + readable /dev/input/event*.
            if (HasDirectInputAccess(ref startupSnapshot))
            {
                Log.Information("[LinuxDisplaySessionService] Flatpak on Wayland without daemon. Using direct device access.");
                return true;
            }

            reason = "Wayland direct mode requires /dev/uinput write access and readable /dev/input/event* devices.";
            Log.Warning("[LinuxDisplaySessionService] {Reason}", reason);
            return false;
        }

        private bool HasDaemonHandshakeAccess(ref LinuxInputCapabilitySnapshot? snapshot)
        {
            try
            {
                snapshot ??= _snapshotProvider.CaptureSnapshot(DaemonHandshakeStartupBudget);
                if (snapshot.Value.DaemonHandshakeSucceeded)
                {
                    return true;
                }

                if (snapshot.Value.DaemonHandshakeTimedOut)
                {
                    Log.Warning(
                        "[LinuxDisplaySessionService] Daemon handshake probe exceeded startup budget ({BudgetMs}ms). Continuing without blocking UI thread.",
                        DaemonHandshakeStartupBudget.TotalMilliseconds);
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[LinuxDisplaySessionService] Daemon handshake probe failed unexpectedly");
                return false;
            }
        }

        private bool HasDirectInputAccess(ref LinuxInputCapabilitySnapshot? snapshot)
        {
            try
            {
                snapshot ??= _snapshotProvider.CaptureSnapshot(DaemonHandshakeProbeTimeout);
                return snapshot.Value.HasDirectInputAccess;
            }
            catch
            {
                return false;
            }
        }
    }
}
