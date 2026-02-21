using System;
using System.IO;
using System.Threading;
using CrossMacro.Core.Ipc;
using CrossMacro.Platform.Linux.Ipc;
using Serilog;

namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// Detects system capabilities and determines the appropriate input provider mode.
/// Implements thread-safe, cached detection for daemon connectivity and uinput access.
/// </summary>
public class LinuxInputCapabilityDetector : ILinuxInputCapabilityDetector
{
    private InputProviderMode? _cachedMode;
    private bool _canConnectToDaemon;
    private DateTime _lastDaemonProbeUtc = DateTime.MinValue;
    private DateTime _lastSuccessfulDaemonProbeUtc = DateTime.MinValue;
    private int _consecutiveDaemonProbeFailures;
    private bool? _canUseDirectUInput;
    private DateTime _lastModeResolutionUtc = DateTime.MinValue;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, bool> _canOpenForWrite;
    private readonly Func<string, bool> _daemonHandshakeProbe;
    private readonly Func<DateTime> _utcNow;
    private readonly Lock _lock = new();
    private static readonly TimeSpan DaemonProbeTtl = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ModeResolutionTtl = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DaemonSuccessGracePeriod = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DaemonHandshakeProbeTimeout = TimeSpan.FromSeconds(5);
    private const int MaxConsecutiveDaemonFailuresBeforeFallback = 3;

    public LinuxInputCapabilityDetector()
        : this(File.Exists, CanOpenForWrite, ProbeDaemonHandshake, static () => DateTime.UtcNow)
    {
    }

    public LinuxInputCapabilityDetector(
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, bool> daemonHandshakeProbe,
        Func<DateTime> utcNow)
    {
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _canOpenForWrite = canOpenForWrite ?? throw new ArgumentNullException(nameof(canOpenForWrite));
        _daemonHandshakeProbe = daemonHandshakeProbe ?? throw new ArgumentNullException(nameof(daemonHandshakeProbe));
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }
    
    public bool CanConnectToDaemon
    {
        get
        {
            using (_lock.EnterScope())
            {
                var now = _utcNow();
                if (_lastDaemonProbeUtc != DateTime.MinValue &&
                    (now - _lastDaemonProbeUtc) <= DaemonProbeTtl)
                {
                    return _canConnectToDaemon;
                }

                RefreshDaemonConnectivity(now);

                return _canConnectToDaemon;
            }
        }
    }

    public bool CanUseDirectUInput
    {
        get
        {
            if (_canUseDirectUInput.HasValue)
            {
                return _canUseDirectUInput.Value;
            }

            using (_lock.EnterScope())
            {
                if (_canUseDirectUInput.HasValue)
                {
                    return _canUseDirectUInput.Value;
                }

                try
                {
                    _canUseDirectUInput = ProbeDirectUInputAccess();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[LinuxInputCapabilityDetector] Failed to check uinput write access");
                    _canUseDirectUInput = false;
                }

                return _canUseDirectUInput.Value;
            }
        }
    }

    public InputProviderMode DetermineMode()
    {
        using (_lock.EnterScope())
        {
            var now = _utcNow();

            if (_lastDaemonProbeUtc == DateTime.MinValue ||
                (now - _lastDaemonProbeUtc) > DaemonProbeTtl)
            {
                RefreshDaemonConnectivity(now);
            }

            if (!_canUseDirectUInput.HasValue)
            {
                try
                {
                    _canUseDirectUInput = ProbeDirectUInputAccess();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[LinuxInputCapabilityDetector] Failed probing uinput write access");
                    _canUseDirectUInput = false;
                }
            }

            if (_cachedMode.HasValue &&
                _lastModeResolutionUtc != DateTime.MinValue &&
                (now - _lastModeResolutionUtc) <= ModeResolutionTtl)
            {
                return _cachedMode.Value;
            }

            if (_canConnectToDaemon)
            {
                _cachedMode = InputProviderMode.Daemon;
                _lastModeResolutionUtc = now;
                return InputProviderMode.Daemon;
            }

            if (ShouldKeepDaemonModeDuringTransientFailure(now))
            {
                Log.Warning(
                    "[LinuxInputCapabilityDetector] Daemon probe failed ({FailureCount}/{MaxFailures}) but recent daemon success is within grace window ({GraceSeconds}s). Keeping DAEMON mode.",
                    _consecutiveDaemonProbeFailures,
                    MaxConsecutiveDaemonFailuresBeforeFallback,
                    DaemonSuccessGracePeriod.TotalSeconds);
                _cachedMode = InputProviderMode.Daemon;
                _lastModeResolutionUtc = now;
                return InputProviderMode.Daemon;
            }

            if (!_canUseDirectUInput.Value && IsDaemonSocketPresent())
            {
                Log.Warning(
                    "[LinuxInputCapabilityDetector] Daemon handshake probe failed, but daemon socket is present and direct uinput is unavailable. Keeping DAEMON mode to avoid unusable LEGACY fallback.");
                _cachedMode = InputProviderMode.Daemon;
                _lastModeResolutionUtc = now;
                return InputProviderMode.Daemon;
            }

            if (_canUseDirectUInput.Value)
            {
                Log.Warning(
                    "[LinuxInputCapabilityDetector] Daemon unavailable, but uinput is writable ({Primary}, {Alternate}). Using LEGACY mode.",
                    LinuxConstants.UInputDevicePath,
                    LinuxConstants.UInputAlternatePath);
                _cachedMode = InputProviderMode.Legacy;
                _lastModeResolutionUtc = now;
                return InputProviderMode.Legacy;
            }

            Log.Warning(
                "[LinuxInputCapabilityDetector] Neither daemon handshake nor uinput write access available ({Primary}, {Alternate}). Defaulting to LEGACY mode.",
                LinuxConstants.UInputDevicePath,
                LinuxConstants.UInputAlternatePath);
            _cachedMode = InputProviderMode.Legacy;
            _lastModeResolutionUtc = now;
            return InputProviderMode.Legacy;
        }
    }

    private void RefreshDaemonConnectivity(DateTime now)
    {
        if (_lastSuccessfulDaemonProbeUtc != DateTime.MinValue && IsDaemonSocketPresent())
        {
            // Once daemon mode has worked, prefer socket presence over repeated handshake probes.
            // Handshake probes open new daemon connections and can trigger transient polkit failures.
            _canConnectToDaemon = true;
            _lastDaemonProbeUtc = now;
            _consecutiveDaemonProbeFailures = 0;
            return;
        }

        _canConnectToDaemon = ProbeDaemonSocketAndHandshake();
        _lastDaemonProbeUtc = now;

        if (_canConnectToDaemon)
        {
            _lastSuccessfulDaemonProbeUtc = now;
            _consecutiveDaemonProbeFailures = 0;
            return;
        }

        _consecutiveDaemonProbeFailures++;
    }

    private bool IsDaemonSocketPresent()
    {
        return _fileExists(IpcProtocol.DefaultSocketPath) || _fileExists(IpcProtocol.FallbackSocketPath);
    }

    private bool ShouldKeepDaemonModeDuringTransientFailure(DateTime now)
    {
        if (_lastSuccessfulDaemonProbeUtc == DateTime.MinValue)
        {
            return false;
        }

        if ((now - _lastSuccessfulDaemonProbeUtc) > DaemonSuccessGracePeriod)
        {
            return false;
        }

        return _consecutiveDaemonProbeFailures < MaxConsecutiveDaemonFailuresBeforeFallback;
    }

    private bool ProbeDirectUInputAccess()
    {
        return _canOpenForWrite(LinuxConstants.UInputDevicePath) ||
               _canOpenForWrite(LinuxConstants.UInputAlternatePath);
    }

    private bool ProbeDaemonSocketAndHandshake()
    {
        try
        {
            string? socketPath = ResolveAvailableSocketPath();
            if (socketPath == null)
            {
                return false;
            }

            return _daemonHandshakeProbe(socketPath);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[LinuxInputCapabilityDetector] Failed probing daemon connectivity");
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

    private static bool CanOpenForWrite(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using (File.OpenWrite(path))
            {
                return true;
            }
        }
        catch
        {
            return false;
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
        catch (Exception ex)
        {
            Log.Debug(ex, "[LinuxInputCapabilityDetector] Daemon handshake probe failed for {SocketPath}", socketPath);
            return false;
        }
    }
}
