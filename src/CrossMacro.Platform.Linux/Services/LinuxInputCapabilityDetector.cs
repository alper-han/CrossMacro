using System;
using System.IO;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Core.Logging;
using CrossMacro.Infrastructure.Linux.Native.Evdev;
using CrossMacro.Platform.Abstractions.Diagnostics;

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
    private bool? _canReadInputEvents;
    private DateTime _lastModeResolutionUtc = DateTime.MinValue;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, bool> _canOpenForWrite;
    private readonly ILinuxInputDeviceAccessProbe _inputDeviceAccessProbe;
    private readonly Func<string, TimeSpan, DaemonHandshakeProbeResult> _daemonHandshakeProbe;
    private readonly Func<DateTime> _utcNow;
    private readonly Lock _lock = new();
    private static readonly TimeSpan DaemonProbeTtl = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ModeResolutionTtl = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DaemonSuccessGracePeriod = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DaemonHandshakeProbeTimeout = TimeSpan.FromSeconds(5);
    private const int MaxConsecutiveDaemonFailuresBeforeFallback = 3;

    public LinuxInputCapabilityDetector()
        : this(
            File.Exists,
            LinuxInputProbeUtilities.CanOpenForWrite,
            new LinuxInputDeviceAccessProbe(),
            ProbeDaemonHandshake,
            static () => DateTime.UtcNow)
    {
    }

    public LinuxInputCapabilityDetector(
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, bool> canOpenForRead,
        Func<string, bool> daemonHandshakeProbe,
        Func<string[]> getInputEventCandidates,
        Func<DateTime> utcNow)
        : this(
            fileExists,
            canOpenForWrite,
            new LinuxInputDeviceAccessProbe(() => LinuxInputProbeUtilities.HasReadableInputEventAccess(canOpenForRead, getInputEventCandidates)),
            (socketPath, _) => daemonHandshakeProbe(socketPath)
                ? DaemonHandshakeProbeResult.Success()
                : DaemonHandshakeProbeResult.Failed(),
            utcNow)
    {
    }

    public LinuxInputCapabilityDetector(
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, bool> canOpenForRead,
        Func<string, TimeSpan, DaemonHandshakeProbeResult> daemonHandshakeProbe,
        Func<string[]> getInputEventCandidates,
        Func<DateTime> utcNow)
        : this(
            fileExists,
            canOpenForWrite,
            new LinuxInputDeviceAccessProbe(() => LinuxInputProbeUtilities.HasReadableInputEventAccess(canOpenForRead, getInputEventCandidates)),
            daemonHandshakeProbe,
            utcNow)
    {
    }

    internal LinuxInputCapabilityDetector(
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, bool> canOpenForRead,
        Func<bool> hasUsableReadableInputDevices,
        Func<string, TimeSpan, DaemonHandshakeProbeResult> daemonHandshakeProbe,
        Func<string[]> getInputEventCandidates,
        Func<DateTime> utcNow)
        : this(
            fileExists,
            canOpenForWrite,
            new LinuxInputDeviceAccessProbe(hasUsableReadableInputDevices),
            daemonHandshakeProbe,
            utcNow)
    {
    }

    internal LinuxInputCapabilityDetector(
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        ILinuxInputDeviceAccessProbe inputDeviceAccessProbe,
        Func<string, TimeSpan, DaemonHandshakeProbeResult> daemonHandshakeProbe,
        Func<DateTime> utcNow)
    {
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _canOpenForWrite = canOpenForWrite ?? throw new ArgumentNullException(nameof(canOpenForWrite));
        _inputDeviceAccessProbe = inputDeviceAccessProbe ?? throw new ArgumentNullException(nameof(inputDeviceAccessProbe));
        _daemonHandshakeProbe = daemonHandshakeProbe ?? throw new ArgumentNullException(nameof(daemonHandshakeProbe));
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public readonly record struct DaemonHandshakeProbeResult(bool Succeeded, bool TimedOut, Exception? Failure, LinuxDaemonHandshakeStatus Status)
    {
        public static DaemonHandshakeProbeResult Success()
        {
            return new(true, false, null, LinuxDaemonHandshakeStatus.Success);
        }

        public static DaemonHandshakeProbeResult Failed(Exception? failure = null)
        {
            return new(false, false, failure, LinuxDaemonHandshakeTransport.MapFailure(failure));
        }

        public static DaemonHandshakeProbeResult Failed(LinuxDaemonHandshakeStatus status, Exception? failure = null)
        {
            if (status == LinuxDaemonHandshakeStatus.Success)
            {
                throw new ArgumentException("Use Success for successful daemon handshakes.", nameof(status));
            }

            return new(false, status == LinuxDaemonHandshakeStatus.Timeout, failure, status);
        }

        public static DaemonHandshakeProbeResult Timeout(Exception? failure = null)
        {
            return new(false, true, failure, LinuxDaemonHandshakeStatus.Timeout);
        }
    }

    internal static DaemonHandshakeProbeResult ProbeDaemonHandshakeWithinBudget(string socketPath, TimeSpan timeout)
    {
        return MapProbeResult(LinuxDaemonHandshakeTransport.ProbeWithinBudget(socketPath, timeout), socketPath);
    }

    private static DaemonHandshakeProbeResult ProbeDaemonHandshake(string socketPath, TimeSpan timeout)
    {
        return ProbeDaemonHandshakeWithinBudget(socketPath, timeout);
    }

    private static DaemonHandshakeProbeResult MapProbeResult(LinuxDaemonHandshakeTransport.ProbeResult result, string socketPath)
    {
        if (result.TimedOut)
        {
            Log.Debug(
                result.Failure ?? new TimeoutException("Daemon handshake probe timed out."),
                "[LinuxInputCapabilityDetector] Daemon handshake probe timed out for {SocketPath}",
                socketPath);
            return DaemonHandshakeProbeResult.Timeout(result.Failure);
        }

        if (result.Succeeded)
        {
            return DaemonHandshakeProbeResult.Success();
        }

        if (result.Failure is not null)
        {
            Log.Debug(result.Failure, "[LinuxInputCapabilityDetector] Daemon handshake probe failed for {SocketPath}", socketPath);
        }

        return DaemonHandshakeProbeResult.Failed(result.Failure);
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

    public bool CanReadInputEvents
    {
        get
        {
            if (_canReadInputEvents.HasValue)
            {
                return _canReadInputEvents.Value;
            }

            using (_lock.EnterScope())
            {
                if (_canReadInputEvents.HasValue)
                {
                    return _canReadInputEvents.Value;
                }

                try
                {
                    _canReadInputEvents = ProbeReadableInputEventAccess();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[LinuxInputCapabilityDetector] Failed checking readable input events");
                    _canReadInputEvents = false;
                }

                return _canReadInputEvents.Value;
            }
        }
    }

    public LinuxInputCapabilitySnapshot GetSnapshot()
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

            if (!_canReadInputEvents.HasValue)
            {
                try
                {
                    _canReadInputEvents = ProbeReadableInputEventAccess();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[LinuxInputCapabilityDetector] Failed probing readable input events");
                    _canReadInputEvents = false;
                }
            }

            return new LinuxInputCapabilitySnapshot(
                ResolvedSocketPath: _resolvedSocketPath,
                DaemonSocketExists: _daemonSocketExists,
                DaemonHandshakeSucceeded: _canConnectToDaemon,
                DaemonHandshakeTimedOut: _lastDaemonHandshakeTimedOut,
                CanUseDirectUInput: _canUseDirectUInput ?? false,
                CanReadInputEvents: _canReadInputEvents ?? false,
                DaemonHandshakeDiagnostic: _lastDaemonHandshakeDiagnostic);
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

            // Daemon is unavailable, so we probe fallback capabilities to decide the mode
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

            if (!_canReadInputEvents.HasValue)
            {
                try
                {
                    _canReadInputEvents = ProbeReadableInputEventAccess();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[LinuxInputCapabilityDetector] Failed probing readable input events");
                    _canReadInputEvents = false;
                }
            }

            var canUseDirectUInput = _canUseDirectUInput ?? false;

            if (!canUseDirectUInput && ShouldKeepDaemonModeDuringTransientFailure(now))
            {
                Log.Warning(
                    "[LinuxInputCapabilityDetector] Daemon probe failed ({FailureCount}/{MaxFailures}) but recent daemon success is within grace window ({GraceSeconds}s) and direct uinput fallback is unavailable. Keeping DAEMON mode.",
                    _consecutiveDaemonProbeFailures,
                    MaxConsecutiveDaemonFailuresBeforeFallback,
                    DaemonSuccessGracePeriod.TotalSeconds);
                _cachedMode = InputProviderMode.Daemon;
                _lastModeResolutionUtc = now;
                return InputProviderMode.Daemon;
            }

            if (!canUseDirectUInput && _daemonSocketExists)
            {
                Log.Warning(
                    "[LinuxInputCapabilityDetector] Daemon socket is present but handshake failed and direct uinput is unavailable. Returning NONE mode for fail-fast handling.");
                _cachedMode = InputProviderMode.None;
                _lastModeResolutionUtc = now;
                return InputProviderMode.None;
            }

            if (canUseDirectUInput)
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
                "[LinuxInputCapabilityDetector] Neither daemon handshake nor uinput write access available ({Primary}, {Alternate}). Returning NONE mode for fail-fast handling.",
                LinuxConstants.UInputDevicePath,
                LinuxConstants.UInputAlternatePath);
            _cachedMode = InputProviderMode.None;
            _lastModeResolutionUtc = now;
            return InputProviderMode.None;
        }
    }

    public void InvalidateCache()
    {
        using (_lock.EnterScope())
        {
            _cachedMode = null;
            _canConnectToDaemon = false;
            _resolvedSocketPath = null;
            _daemonSocketExists = false;
            _lastDaemonHandshakeTimedOut = false;
            _lastDaemonHandshakeDiagnostic = null;
            _lastDaemonProbeUtc = DateTime.MinValue;
            _lastSuccessfulDaemonProbeUtc = DateTime.MinValue;
            _consecutiveDaemonProbeFailures = 0;
            _canUseDirectUInput = null;
            _canReadInputEvents = null;
            _lastModeResolutionUtc = DateTime.MinValue;
        }
    }

    private void RefreshDaemonConnectivity(DateTime now)
    {
        var socketPath = ResolveAvailableSocketPath();
        _resolvedSocketPath = socketPath;
        _daemonSocketExists = socketPath is not null;

        if (_lastSuccessfulDaemonProbeUtc != DateTime.MinValue && _daemonSocketExists)
        {
            // Once daemon mode has worked, prefer socket presence over repeated
            // handshake probes. The daemon currently serves one long-lived session
            // at a time, so a second probe can time out while the primary IPC client
            // is healthy and still connected.
            _canConnectToDaemon = true;
            _lastDaemonProbeUtc = now;
            _lastDaemonHandshakeTimedOut = false;
            _lastDaemonHandshakeDiagnostic = CreateDaemonHandshakeDiagnostic(
                _resolvedSocketPath,
                DaemonHandshakeProbeResult.Success(),
                DaemonHandshakeProbeTimeout);
            _lastSuccessfulDaemonProbeUtc = now;
            _consecutiveDaemonProbeFailures = 0;
            return;
        }

        var probeResult = ProbeDaemonSocketAndHandshake();
        _canConnectToDaemon = probeResult.Succeeded;
        _lastDaemonProbeUtc = now;
        _lastDaemonHandshakeTimedOut = probeResult.TimedOut;
        _lastDaemonHandshakeDiagnostic = CreateDaemonHandshakeDiagnostic(
            _resolvedSocketPath,
            probeResult,
            DaemonHandshakeProbeTimeout);

        if (_canConnectToDaemon)
        {
            _lastSuccessfulDaemonProbeUtc = now;
            _consecutiveDaemonProbeFailures = 0;
            return;
        }

        _consecutiveDaemonProbeFailures++;

        if (probeResult.TimedOut)
        {
            Log.Warning(
                "[LinuxInputCapabilityDetector] Daemon handshake probe timed out after {TimeoutMs}ms.",
                DaemonHandshakeProbeTimeout.TotalMilliseconds);
        }
    }

    private bool IsDaemonSocketPresent()
    {
        return ResolveAvailableSocketPath() is not null;
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
        return LinuxInputProbeUtilities.HasUInputWriteAccess(_canOpenForWrite);
    }

    private bool ProbeReadableInputEventAccess()
    {
        return _inputDeviceAccessProbe.HasUsableReadableInputDevices();
    }

    private DaemonHandshakeProbeResult ProbeDaemonSocketAndHandshake()
    {
        try
        {
            string? socketPath = _resolvedSocketPath;
            if (socketPath == null)
            {
                socketPath = ResolveAvailableSocketPath();
                _resolvedSocketPath = socketPath;
                _daemonSocketExists = socketPath is not null;
            }

            if (socketPath == null)
            {
                return DaemonHandshakeProbeResult.Failed(LinuxDaemonHandshakeStatus.MissingSocket);
            }

            return ProbeDaemonHandshake(socketPath);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[LinuxInputCapabilityDetector] Failed probing daemon connectivity");
            return DaemonHandshakeProbeResult.Failed(ex);
        }
    }

    private DaemonHandshakeProbeResult ProbeDaemonHandshake(string socketPath)
    {
        return _daemonHandshakeProbe(socketPath, DaemonHandshakeProbeTimeout);
    }

    private string? ResolveAvailableSocketPath()
    {
        return LinuxInputProbeUtilities.ResolveAvailableSocketPath(_fileExists);
    }


    private static LinuxDaemonHandshakeProbeResult CreateDaemonHandshakeDiagnostic(
        string? socketPath,
        DaemonHandshakeProbeResult probeResult,
        TimeSpan timeout)
    {
        var resolvedSocketPath = socketPath ?? IpcProtocol.DefaultSocketPath;
        return probeResult.Succeeded
            ? LinuxDaemonHandshakeProbeResult.Success(resolvedSocketPath, timeout)
            : LinuxDaemonHandshakeProbeResult.Failed(
                resolvedSocketPath,
                timeout,
                probeResult.Status,
                probeResult.Failure?.Message,
                probeResult.Failure);
    }

    private string? _resolvedSocketPath;
    private bool _daemonSocketExists;
    private bool _lastDaemonHandshakeTimedOut;
    private LinuxDaemonHandshakeProbeResult? _lastDaemonHandshakeDiagnostic;
}
