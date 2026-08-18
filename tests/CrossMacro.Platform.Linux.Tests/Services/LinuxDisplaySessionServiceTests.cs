namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxDisplaySessionServiceTests
{
    [Fact]
    public void IsSessionSupported_WhenNotFlatpak_ReturnsTrueWithoutInputProbe()
    {
        var provider = new RecordingSnapshotProvider(InputSnapshot(directReady: false));
        var service = new LinuxDisplaySessionService(provider, Environment(flatpak: false, sessionType: "wayland"));

        var supported = service.IsSessionSupported(out var reason);

        Assert.True(supported);
        Assert.Empty(reason);
        Assert.Equal(0, provider.CaptureCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("tty")]
    public void IsSessionSupported_WhenFlatpakSessionIsUnsupported_ReturnsFalse(string? sessionType)
    {
        var provider = new RecordingSnapshotProvider(InputSnapshot(directReady: true));
        var service = new LinuxDisplaySessionService(provider, Environment(flatpak: true, sessionType));

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("Unsupported Flatpak session", reason, StringComparison.Ordinal);
        Assert.Equal(0, provider.CaptureCount);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void IsSessionSupported_WhenFlatpakWayland_RequiresDirectInputAccess(
        bool canUseDirectUInput,
        bool canReadInputEvents,
        bool expected)
    {
        var provider = new RecordingSnapshotProvider(InputSnapshot(canUseDirectUInput, canReadInputEvents));
        var service = new LinuxDisplaySessionService(provider, Environment(flatpak: true, sessionType: "wayland"));

        var supported = service.IsSessionSupported(out var reason);

        Assert.Equal(expected, supported);
        Assert.Equal(expected ? string.Empty : "Wayland direct mode requires /dev/uinput write access and readable /dev/input/event* devices.", reason);
        Assert.Equal(1, provider.CaptureCount);
        Assert.Equal(TimeSpan.Zero, provider.LastBudget);
    }

    [Fact]
    public void IsSessionSupported_WhenFlatpakWaylandDaemonIsAvailable_DoesNotAcceptItWithoutDirectAccess()
    {
        var provider = new RecordingSnapshotProvider(new LinuxInputCapabilitySnapshot(
            ResolvedSocketPath: IpcProtocol.DefaultSocketPath,
            DaemonSocketExists: true,
            DaemonHandshakeSucceeded: true,
            DaemonHandshakeTimedOut: false,
            CanUseDirectUInput: false,
            CanReadInputEvents: false));
        var service = new LinuxDisplaySessionService(provider, Environment(flatpak: true, sessionType: "wayland"));

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("direct mode", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TimeSpan.Zero, provider.LastBudget);
    }

    [Theory]
    [InlineData("x11", null)]
    [InlineData(null, "x11")]
    public void IsSessionSupported_WhenFlatpakX11_ReturnsTrueWithoutInputProbe(
        string? sessionType,
        string? display)
    {
        var provider = new RecordingSnapshotProvider(InputSnapshot(directReady: false));
        var environment = Environment(flatpak: true, sessionType) with { Display = display };
        var service = new LinuxDisplaySessionService(provider, environment);

        var supported = service.IsSessionSupported(out var reason);

        Assert.True(supported);
        Assert.Empty(reason);
        Assert.Equal(0, provider.CaptureCount);
    }

    [Fact]
    public async Task IsSessionSupportedAsync_WhenCanceled_PropagatesCancellation()
    {
        var provider = new RecordingSnapshotProvider(InputSnapshot(directReady: true));
        var service = new LinuxDisplaySessionService(provider, Environment(flatpak: true, sessionType: "wayland"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await service.IsSessionSupportedAsync(cancellation.Token));
    }

    private static LinuxEnvironmentSnapshot Environment(bool flatpak, string? sessionType) =>
        new(
            FlatpakId: flatpak ? "io.github.alper_han.crossmacro" : null,
            AppImage: null,
            SessionType: sessionType,
            WaylandDisplay: string.Equals(sessionType, "wayland", StringComparison.Ordinal) ? "wayland-0" : null,
            Display: null,
            CurrentDesktop: null,
            GdmSession: null,
            HyprlandInstanceSignature: null,
            RuntimeDir: null,
            WayfireSocket: null,
            SwaySocket: null,
            WindowButtons: null);

    private static LinuxInputCapabilitySnapshot InputSnapshot(bool directReady) =>
        InputSnapshot(directReady, directReady);

    private static LinuxInputCapabilitySnapshot InputSnapshot(bool canUseDirectUInput, bool canReadInputEvents) =>
        new(
            ResolvedSocketPath: null,
            DaemonSocketExists: false,
            DaemonHandshakeSucceeded: false,
            DaemonHandshakeTimedOut: false,
            CanUseDirectUInput: canUseDirectUInput,
            CanReadInputEvents: canReadInputEvents);

    private sealed class RecordingSnapshotProvider(LinuxInputCapabilitySnapshot snapshot)
        : ILinuxInputCapabilitySnapshotProvider
    {
        public int CaptureCount { get; private set; }
        public TimeSpan? LastBudget { get; private set; }

        public LinuxInputCapabilitySnapshot CaptureSnapshot(TimeSpan daemonHandshakeBudget)
        {
            CaptureCount++;
            LastBudget = daemonHandshakeBudget;
            return snapshot;
        }

        public ValueTask<LinuxInputCapabilitySnapshot> CaptureSnapshotAsync(
            TimeSpan daemonHandshakeBudget,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(CaptureSnapshot(daemonHandshakeBudget));
        }
    }
}
