namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class DaemonFallbackQuickSetupServiceTests
{
    [LinuxFact]
    public async Task ShouldPromptAsync_WhenDaemonIsUnavailableAndDirectCaptureIsUnavailable_ReturnsTrue()
    {
        var snapshotProvider = new FakeSnapshotProvider(CreateSnapshot(hasDirectInputAccess: false));
        var service = CreateService(snapshotProvider);

        var shouldPrompt = await service.ShouldPromptAsync(CancellationToken.None);

        Assert.True(shouldPrompt);
    }

    [LinuxFact]
    public async Task ShouldPromptAsync_WhenDirectCaptureIsAvailable_ReturnsFalse()
    {
        var snapshotProvider = new FakeSnapshotProvider(CreateSnapshot(hasDirectInputAccess: true));
        var service = CreateService(snapshotProvider);

        var shouldPrompt = await service.ShouldPromptAsync(CancellationToken.None);

        Assert.False(shouldPrompt);
    }

    [LinuxFact]
    public async Task ShouldPromptAsync_WhenUInputIsWritableButInputEventsAreUnreadable_ReturnsTrue()
    {
        var snapshotProvider = new FakeSnapshotProvider(CreateSnapshot(
            canUseDirectUInput: true,
            canReadInputEvents: false));
        var service = CreateService(snapshotProvider);

        var shouldPrompt = await service.ShouldPromptAsync(CancellationToken.None);

        Assert.True(shouldPrompt);
    }

    [LinuxFact]
    public async Task ShouldPromptAsync_WhenX11NativeBackendCanBeUsed_ReturnsFalse()
    {
        var snapshotProvider = new FakeSnapshotProvider(CreateSnapshot(
            hasDirectInputAccess: false,
            compositor: CompositorType.X11));
        var service = CreateService(snapshotProvider);

        var shouldPrompt = await service.ShouldPromptAsync(CancellationToken.None);

        Assert.False(shouldPrompt);
    }


    private static DaemonFallbackQuickSetupService CreateService(
        FakeSnapshotProvider snapshotProvider,
        Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>>? runProcess = null)
    {
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => 1000, () => "0 0 4294967295", _ => ValueTask.FromResult<uint?>(null)),
            runProcess ?? ((_, _) => Task.FromResult((0, string.Empty, string.Empty))));
        var launcher = QuickSetupReadinessTests.CreateLauncher();

        return new DaemonFallbackQuickSetupService(
            snapshotProvider,
            executor,
            launcher,
            Substitute.For<ILinuxInputCapabilityDetector>());
    }

    private static LinuxCapabilitySnapshot CreateSnapshot(
        bool hasDirectInputAccess = false,
        bool? canUseDirectUInput = null,
        bool? canReadInputEvents = null,
        CompositorType compositor = CompositorType.KDE)
    {
        var environment = new LinuxEnvironmentSnapshot(
            FlatpakId: null,
            AppImage: null,
            SessionType: "wayland",
            WaylandDisplay: "wayland-0",
            Display: null,
            CurrentDesktop: "KDE",
            GdmSession: null,
            HyprlandInstanceSignature: null,
            RuntimeDir: "/run/user/1000",
            WayfireSocket: null,
            SwaySocket: null,
            WindowButtons: null);
        var input = new LinuxInputCapabilitySnapshot(
            IpcProtocol.DefaultSocketPath,
            DaemonSocketExists: true,
            DaemonHandshakeSucceeded: false,
            DaemonHandshakeTimedOut: false,
            CanUseDirectUInput: canUseDirectUInput ?? hasDirectInputAccess,
            CanReadInputEvents: canReadInputEvents ?? hasDirectInputAccess);

        return new LinuxCapabilitySnapshot(environment, compositor, input, default);
    }

    private sealed class FakeSnapshotProvider(LinuxCapabilitySnapshot snapshot) : ILinuxCapabilitySnapshotProvider
    {
        private readonly LinuxCapabilitySnapshot _snapshot = snapshot;

        public int InvalidateCallCount { get; private set; }

        public LinuxCapabilitySnapshot GetSnapshot() => _snapshot;

        public void InvalidateScreenReadingCache() { }

        public void InvalidateDirectInputCache() { }

        public void InvalidateCache()
        {
            InvalidateCallCount++;
        }
    }

}
