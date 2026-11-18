
namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class AppImageQuickSetupServiceTests
{
    [LinuxFact]
    public void IsApplicable_WhenAppImageWayland_ShouldReturnTrue()
    {
        var service = CreateService(AppImageWayland(), canUseDirectUInput: false, canReadInputEvents: false);

        Assert.True(service.IsApplicable());
    }

    [Fact]
    public void IsApplicable_WhenFlatpak_ShouldReturnFalse()
    {
        var service = CreateService(
            AppImageWayland() with { FlatpakId = "io.github.alper_han.crossmacro" },
            canUseDirectUInput: false,
            canReadInputEvents: false);

        Assert.False(service.IsApplicable());
    }

    [Fact]
    public void ShouldPrompt_WhenNeitherUInputNorInputEventsAreAccessible_ShouldReturnTrue()
    {
        var service = CreateService(AppImageWayland(), canUseDirectUInput: false, canReadInputEvents: false);

        Assert.True(service.ShouldPrompt());
    }

    [Fact]
    public void ShouldPrompt_WhenUInputIsWritableButNoUsableInputDevices_ShouldReturnTrue()
    {
        var service = CreateService(AppImageWayland(), canUseDirectUInput: true, canReadInputEvents: false);

        Assert.True(service.ShouldPrompt());
    }

    [Fact]
    public void ShouldPrompt_WhenHostDaemonIsAvailableButDirectAccessIsMissing_ReturnsTrue()
    {
        var service = CreateService(AppImageWayland(), canUseDirectUInput: false, canReadInputEvents: false, daemonHandshakeSucceeded: true);

        Assert.True(service.ShouldPrompt());
    }

    [Fact]
    public void ShouldPrompt_WhenDirectAccessIsReady_ReturnsFalse()
    {
        var service = CreateService(AppImageWayland(), canUseDirectUInput: true, canReadInputEvents: true);

        Assert.False(service.ShouldPrompt());
    }

    private static LinuxEnvironmentSnapshot AppImageWayland() =>
        new(
            FlatpakId: null,
            AppImage: "/tmp/CrossMacro.AppImage",
            SessionType: "wayland",
            WaylandDisplay: "wayland-0",
            Display: null,
            CurrentDesktop: null,
            GdmSession: null,
            HyprlandInstanceSignature: null,
            RuntimeDir: null,
            WayfireSocket: null,
            SwaySocket: null,
            WindowButtons: null);

    private static AppImageQuickSetupService CreateService(
        LinuxEnvironmentSnapshot environment,
        bool canUseDirectUInput,
        bool canReadInputEvents,
        bool daemonHandshakeSucceeded = false)
    {
        var input = new LinuxInputCapabilitySnapshot(
            ResolvedSocketPath: null,
            DaemonSocketExists: daemonHandshakeSucceeded,
            DaemonHandshakeSucceeded: daemonHandshakeSucceeded,
            DaemonHandshakeTimedOut: false,
            CanUseDirectUInput: canUseDirectUInput,
            CanReadInputEvents: canReadInputEvents);
        var snapshots = Substitute.For<ILinuxCapabilitySnapshotProvider>();
        snapshots.GetSnapshot().Returns(new LinuxCapabilitySnapshot(environment, CompositorType.Unknown, input, default));
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => 1000, () => "0 0 4294967295", _ => ValueTask.FromResult<uint?>(null)),
            (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        return new AppImageQuickSetupService(environment, snapshots, executor, QuickSetupReadinessTests.CreateLauncher());
    }
}
