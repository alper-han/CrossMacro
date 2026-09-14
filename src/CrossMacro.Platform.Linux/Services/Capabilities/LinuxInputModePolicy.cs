namespace CrossMacro.Platform.Linux.Services.Capabilities;

/// <summary>
/// Owns the runtime mode cache and daemon failure hysteresis. The caller serializes
/// access; this policy performs no I/O and never substitutes a raw probe result.
/// </summary>
internal sealed class LinuxInputModePolicy
{
    private InputProviderMode? _cachedMode;
    private DateTime _lastModeResolutionUtc = DateTime.MinValue;
    private DateTime _lastSuccessfulDaemonProbeUtc = DateTime.MinValue;
    private int _consecutiveDaemonProbeFailures;
    private static readonly TimeSpan ModeResolutionTtl = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DaemonSuccessGracePeriod = TimeSpan.FromSeconds(30);
    private const int MaxConsecutiveDaemonFailuresBeforeFallback = 3;

    internal bool HasSuccessfulDaemonHistory => _lastSuccessfulDaemonProbeUtc != DateTime.MinValue;

    internal void ObserveDaemonResult(DateTime now, bool succeeded)
    {
        if (succeeded)
        {
            _lastSuccessfulDaemonProbeUtc = now;
            _consecutiveDaemonProbeFailures = 0;
        }
        else
        {
            _consecutiveDaemonProbeFailures++;
        }
    }

    internal void Invalidate()
    {
        _cachedMode = null;
        _lastModeResolutionUtc = DateTime.MinValue;
        _lastSuccessfulDaemonProbeUtc = DateTime.MinValue;
        _consecutiveDaemonProbeFailures = 0;
    }

    internal InputProviderMode Resolve(DateTime now, bool canConnectToDaemon, bool canUseDirectUInput, bool daemonSocketExists)
    {
        if (_cachedMode is not null &&
            _lastModeResolutionUtc != DateTime.MinValue &&
            (now - _lastModeResolutionUtc) <= ModeResolutionTtl)
        {
            return _cachedMode.Value;
        }

        if (canConnectToDaemon)
        {
            _cachedMode = InputProviderMode.Daemon;
            _lastModeResolutionUtc = now;
            return InputProviderMode.Daemon;
        }

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

        if (!canUseDirectUInput && daemonSocketExists)
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

    private bool ShouldKeepDaemonModeDuringTransientFailure(DateTime now)
    {
        if (_lastSuccessfulDaemonProbeUtc == DateTime.MinValue)
        {
            return false;
        }

        return IsWithinDaemonGracePeriod(
            now,
            _lastSuccessfulDaemonProbeUtc,
            _consecutiveDaemonProbeFailures,
            DaemonSuccessGracePeriod,
            MaxConsecutiveDaemonFailuresBeforeFallback);
    }

    internal static bool IsWithinDaemonGracePeriod(
        DateTime now,
        DateTime lastSuccessfulProbeUtc,
        int consecutiveFailures,
        TimeSpan gracePeriod,
        int maxFailuresBeforeFallback)
    {
        return lastSuccessfulProbeUtc != DateTime.MinValue &&
               (now - lastSuccessfulProbeUtc) <= gracePeriod &&
               consecutiveFailures < maxFailuresBeforeFallback;
    }

}
