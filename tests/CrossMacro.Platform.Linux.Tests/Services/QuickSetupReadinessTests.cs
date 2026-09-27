namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class QuickSetupReadinessTests
{
    [Theory]
    [InlineData("appimage", false, true)]
    [InlineData("appimage", true, false)]
    [InlineData("appimage", true, true)]
    [InlineData("flatpak", false, true)]
    [InlineData("flatpak", true, false)]
    [InlineData("flatpak", true, true)]
    [InlineData("native", false, true)]
    [InlineData("native", true, false)]
    [InlineData("native", true, true)]
    public async Task RunAsync_RequiresFreshDirectAccessRatherThanHelperExitOrDaemon(string provider, bool writable, bool readable)
    {
        var invalidated = false;
        var operationFinished = false;
        var snapshot = CreateSnapshotProvider();
        snapshot.When(x => x.InvalidateCache()).Do(_ => invalidated = true);
        snapshot.GetSnapshot().Returns(_ =>
        {
            if (provider is "native" && operationFinished)
            {
                throw new InvalidOperationException("Do not probe the daemon after setup");
            }
            return CreateSnapshot(invalidated && writable, invalidated && readable);
        });
        var direct = Substitute.For<ILinuxInputCapabilityDetector>();
        direct.CanUseDirectUInput.Returns(_ => invalidated && writable);
        direct.CanReadInputEvents.Returns(_ => invalidated && readable);
        var executor = new LinuxQuickSetupExecutor(new LinuxQuickSetupIdentityResolver(() => 1000, () => "0 0 4294967295", _ => ValueTask.FromResult<uint?>(null)), (_, _) =>
        {
            operationFinished = true;
            return Task.FromResult((0, "host helper completed", string.Empty));
        });
        var result = await RunAsync(provider, snapshot, direct, executor, TestContext.Current.CancellationToken);
        Assert.Equal(writable && readable ? QuickSetupOutcome.Succeeded : QuickSetupOutcome.DeviceAccessUnavailable, result.Outcome);
        Assert.Equal(writable && readable, result.Success);
    }

    [Theory]
    [InlineData("appimage", false)]
    [InlineData("flatpak", false)]
    [InlineData("native", false)]
    [InlineData("appimage", true)]
    [InlineData("flatpak", true)]
    [InlineData("native", true)]
    public async Task RunAsync_WhenOperationFailsOrCancels_DiscardsCachedAccess(string provider, bool cancelled)
    {
        var invalidated = false;
        var snapshot = CreateSnapshotProvider();
        snapshot.When(x => x.InvalidateCache()).Do(_ => invalidated = true);
        snapshot.GetSnapshot().Returns(_ => CreateSnapshot(invalidated, invalidated));
        var executor = new LinuxQuickSetupExecutor(new LinuxQuickSetupIdentityResolver(() => 1000, () => "0 0 4294967295", _ => ValueTask.FromResult<uint?>(null)), (_, token) =>
            cancelled ? Task.FromCanceled<(int, string, string)>(new CancellationToken(canceled: true)) : Task.FromResult((22, string.Empty, "partial ACL failure")));
        if (cancelled)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(provider, snapshot, Substitute.For<ILinuxInputCapabilityDetector>(), executor, TestContext.Current.CancellationToken));
        }
        else
        {
            var result = await RunAsync(provider, snapshot, Substitute.For<ILinuxInputCapabilityDetector>(), executor, TestContext.Current.CancellationToken);
            Assert.False(result.Success);
        }
        var refreshed = snapshot.GetSnapshot().Input;
        Assert.True(refreshed.CanUseDirectUInput);
        Assert.True(refreshed.CanReadInputEvents);
    }

    private static Task<QuickSetupResult> RunAsync(string provider, ILinuxCapabilitySnapshotProvider snapshot, ILinuxInputCapabilityDetector direct, LinuxQuickSetupExecutor executor, CancellationToken token)
        => provider switch
        {
            "appimage" => new AppImageQuickSetupService(snapshot, executor, CreateLauncher()).RunAsync(token),
            "flatpak" => new FlatpakQuickSetupService(static _ => null, executor, CreateLauncher(), snapshot).RunAsync(token),
            "native" => new DaemonFallbackQuickSetupService(snapshot, executor, CreateLauncher(), direct).RunAsync(token),
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

    internal static IPrivilegedHostCommandLauncher CreateLauncher(bool available = true)
    {
        return new FakeLauncher(available);
    }

    internal static ILinuxCapabilitySnapshotProvider CreateSnapshotProvider()
    {
        var provider = Substitute.For<ILinuxCapabilitySnapshotProvider>();
        provider.GetSnapshot().Returns(CreateSnapshot(writable: false, readable: false));
        return provider;
    }

    private sealed class FakeLauncher(bool available) : IPrivilegedHostCommandLauncher
    {
        public ValueTask<LinuxQuickSetupIdentity?> ResolveIdentityAsync(LinuxQuickSetupIdentityResolver resolver, CancellationToken cancellationToken = default)
            => resolver.ResolveAsync(cancellationToken);
        public ValueTask<(ProcessStartInfo? StartInfo, string FailureMessage)> CreateStartInfoAsync(
            string hostScript, LinuxQuickSetupIdentity identity, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<(ProcessStartInfo?, string)>((available ? new("/usr/bin/pkexec") { UseShellExecute = false } : null, "No usable privilege mechanism"));
    }

    private static LinuxCapabilitySnapshot CreateSnapshot(bool writable, bool readable)
    {
        var environment = new LinuxEnvironmentSnapshot(FlatpakId: null, AppImage: null, SessionType: "wayland", WaylandDisplay: "wayland-0", Display: null, CurrentDesktop: "KDE", GdmSession: null, HyprlandInstanceSignature: null, RuntimeDir: "/run/user/1000", WayfireSocket: null, SwaySocket: null, WindowButtons: null);
        var input = new LinuxInputCapabilitySnapshot(ResolvedSocketPath: null, DaemonSocketExists: true, DaemonHandshakeSucceeded: true, DaemonHandshakeTimedOut: false, CanUseDirectUInput: writable, CanReadInputEvents: readable);
        return new LinuxCapabilitySnapshot(environment, CompositorType.KDE, input, default);
    }
}
