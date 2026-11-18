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

    internal void InvalidateModeResolution() => _lastModeResolutionUtc = DateTime.MinValue;

    internal void Invalidate()
    {
        _cachedMode = null;
        _lastModeResolutionUtc = DateTime.MinValue;
        _lastSuccessfulDaemonProbeUtc = DateTime.MinValue;
        _consecutiveDaemonProbeFailures = 0;
    }

    internal InputProviderMode Resolve(DateTime now, bool canConnectToDaemon, bool canUseDirectUInput, bool daemonSocketExists)
    {
        if (_cachedMode is { } cached &&
            _lastModeResolutionUtc != DateTime.MinValue &&
            (now - _lastModeResolutionUtc) <= ModeResolutionTtl)
        {
            return cached;
        }

        var mode = ChooseMode(now, canConnectToDaemon, canUseDirectUInput, daemonSocketExists);
        _cachedMode = mode;
        _lastModeResolutionUtc = now;
        return mode;
    }

    private InputProviderMode ChooseMode(DateTime now, bool canConnectToDaemon, bool canUseDirectUInput, bool daemonSocketExists)
    {
        if (canConnectToDaemon)
        {
            return InputProviderMode.Daemon;
        }

        if (canUseDirectUInput)
        {
            Log.Warning(
                "[LinuxInputModePolicy] Daemon unavailable, but uinput is writable ({Primary}, {Alternate}). Using direct device mode.",
                LinuxSystemPaths.UInputDevicePath,
                LinuxSystemPaths.UInputAlternatePath);
            return InputProviderMode.Legacy;
        }

        if (IsWithinDaemonGracePeriod(
                now,
                _lastSuccessfulDaemonProbeUtc,
                _consecutiveDaemonProbeFailures,
                DaemonSuccessGracePeriod,
                MaxConsecutiveDaemonFailuresBeforeFallback))
        {
            Log.Warning(
                "[LinuxInputModePolicy] Daemon probe failed ({FailureCount}/{MaxFailures}) but recent daemon success is within grace window ({GraceSeconds}s) and direct uinput fallback is unavailable. Keeping daemon mode.",
                _consecutiveDaemonProbeFailures,
                MaxConsecutiveDaemonFailuresBeforeFallback,
                DaemonSuccessGracePeriod.TotalSeconds);
            return InputProviderMode.Daemon;
        }

        Log.Warning(
            daemonSocketExists
                ? "[LinuxInputModePolicy] Daemon socket is present but handshake failed and direct uinput is unavailable ({Primary}, {Alternate}). Failing fast."
                : "[LinuxInputModePolicy] Neither daemon handshake nor uinput write access available ({Primary}, {Alternate}). Failing fast.",
            LinuxSystemPaths.UInputDevicePath,
            LinuxSystemPaths.UInputAlternatePath);
        return InputProviderMode.None;
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
