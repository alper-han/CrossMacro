using CrossMacro.Platform.Linux.Native;
using CrossMacro.Platform.Linux.Native.Evdev;

namespace CrossMacro.Platform.Linux.Tests.Services;

/// <summary>Builds the real input snapshot provider on top of deterministic probes.</summary>
internal static class InputSnapshotTestFactory
{
    internal static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    internal static bool IsDaemonSocket(string path) =>
        string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal);

    internal static bool IsPrimaryUInput(string path) =>
        string.Equals(path, LinuxSystemPaths.UInputDevicePath, StringComparison.Ordinal);

    internal static bool IsAlternateUInput(string path) =>
        string.Equals(path, LinuxSystemPaths.UInputAlternatePath, StringComparison.Ordinal);

    internal static LinuxInputCapabilitySnapshotProvider Create(
        Func<string, bool>? fileExists = null,
        Func<string, bool>? canOpenForWrite = null,
        Func<bool>? hasReadableInputDevices = null,
        Func<string, TimeSpan, CancellationToken, ValueTask<LinuxDaemonHandshakeProbeResult>>? handshake = null,
        Func<DateTime>? utcNow = null,
        bool daemonEnabled = true) =>
        new(
            daemonEnabled,
            fileExists ?? (static _ => false),
            canOpenForWrite ?? (static _ => false),
            new LinuxInputDeviceAccessProbe(hasReadableInputDevices ?? (static () => false)),
            handshake ?? Handshake(Success),
            utcNow ?? (static () => Epoch));

    internal static LinuxDaemonHandshakeProbeResult Success(string path, TimeSpan timeout) =>
        LinuxDaemonHandshakeProbeResult.Success(path, timeout);

    internal static LinuxDaemonHandshakeProbeResult Failure(
        string path,
        TimeSpan timeout,
        LinuxDaemonHandshakeStatus status = LinuxDaemonHandshakeStatus.UnexpectedError,
        Exception? exception = null) =>
        LinuxDaemonHandshakeProbeResult.Failed(path, timeout, status, exception?.Message, exception);

    internal static Func<string, TimeSpan, CancellationToken, ValueTask<LinuxDaemonHandshakeProbeResult>> Handshake(
        Func<string, TimeSpan, LinuxDaemonHandshakeProbeResult> probe) =>
        (path, timeout, _) => ValueTask.FromResult(probe(path, timeout));

    internal static Func<string, TimeSpan, CancellationToken, ValueTask<LinuxDaemonHandshakeProbeResult>> HandshakeThatFails(
        LinuxDaemonHandshakeStatus status = LinuxDaemonHandshakeStatus.UnexpectedError,
        Exception? exception = null) =>
        Handshake((path, timeout) => Failure(path, timeout, status, exception));

    internal static LinuxInputCapabilitySnapshot Snapshot(
        InputProviderMode mode = InputProviderMode.None,
        bool daemonHandshakeSucceeded = false,
        bool canUseDirectUInput = false,
        bool canReadInputEvents = false,
        LinuxDaemonHandshakeProbeResult? handshake = null) =>
        new(
            ResolvedSocketPath: daemonHandshakeSucceeded ? IpcProtocol.DefaultSocketPath : null,
            DaemonSocketExists: daemonHandshakeSucceeded,
            DaemonHandshakeSucceeded: daemonHandshakeSucceeded,
            DaemonHandshakeTimedOut: handshake?.TimedOut ?? false,
            CanUseDirectUInput: canUseDirectUInput,
            CanReadInputEvents: canReadInputEvents,
            DaemonHandshakeDiagnostic: handshake,
            ResolvedMode: mode);

    internal static LinuxEnvironmentSnapshot Environment(string sessionType, string? display = null) =>
        new(
            FlatpakId: null,
            AppImage: null,
            SessionType: sessionType,
            WaylandDisplay: sessionType is "wayland" ? "wayland-0" : null,
            Display: display,
            CurrentDesktop: null,
            GdmSession: null,
            HyprlandInstanceSignature: null,
            RuntimeDir: null,
            WayfireSocket: null,
            SwaySocket: null,
            WindowButtons: null);

    /// <summary>An aggregate provider that reports the given compositor and input snapshot.</summary>
    internal static ILinuxCapabilitySnapshotProvider Capabilities(
        CompositorType compositor,
        Func<LinuxInputCapabilitySnapshot> input)
    {
        var environment = Environment(compositor is CompositorType.X11 ? "x11" : "wayland");
        var provider = Substitute.For<ILinuxCapabilitySnapshotProvider>();
        provider.GetSnapshot().Returns(_ => new LinuxCapabilitySnapshot(environment, compositor, input(), default));
        return provider;
    }

    /// <summary>A provider that always answers with the supplied snapshot and records invalidations.</summary>
    internal sealed class Fixed(LinuxInputCapabilitySnapshot snapshot) : ILinuxInputCapabilitySnapshotProvider
    {
        public int SnapshotCount { get; private set; }
        public int InvalidateCount { get; private set; }
        public int InvalidateDirectCount { get; private set; }

        public LinuxInputCapabilitySnapshot GetSnapshot()
        {
            SnapshotCount++;
            return snapshot;
        }

        public ValueTask<LinuxInputCapabilitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(GetSnapshot());
        }

        public void InvalidateDirectInputCache() => InvalidateDirectCount++;

        public ValueTask<bool> ProbeDirectInputAccessAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(snapshot.HasDirectInputAccess);

        public void InvalidateCache() => InvalidateCount++;
    }
}
