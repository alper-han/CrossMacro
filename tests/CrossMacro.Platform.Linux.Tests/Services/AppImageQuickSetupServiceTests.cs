
namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class AppImageQuickSetupServiceTests
{
    [LinuxFact]
    public void IsApplicable_WhenAppImageWayland_ShouldReturnTrue()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["APPIMAGE"] = "/tmp/CrossMacro.AppImage",
            ["FLATPAK_ID"] = null,
            ["XDG_SESSION_TYPE"] = "wayland",
        };

        var service = CreateService(
            env,
            InputProviderMode.None,
            canReadInputEvents: false,
            effectiveUid: 1000,
            (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        Assert.True(service.IsApplicable());
    }

    [Fact]
    public void ShouldPrompt_WhenCapabilityModeIsNone_ShouldReturnTrue()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["APPIMAGE"] = "/tmp/CrossMacro.AppImage",
            ["FLATPAK_ID"] = null,
            ["XDG_SESSION_TYPE"] = "wayland",
        };

        var service = CreateService(
            env,
            InputProviderMode.None,
            canReadInputEvents: false,
            effectiveUid: 1000,
            (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        Assert.True(service.ShouldPrompt());
    }

    [Fact]
    public void ShouldPrompt_WhenLegacyModeButNoUsableInputDevices_ShouldReturnTrue()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["APPIMAGE"] = "/tmp/CrossMacro.AppImage",
            ["FLATPAK_ID"] = null,
            ["XDG_SESSION_TYPE"] = "wayland",
        };

        var service = CreateService(
            env,
            InputProviderMode.Legacy,
            canReadInputEvents: false,
            effectiveUid: 1000,
            (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        Assert.True(service.ShouldPrompt());
    }

    [Fact]
    public void ShouldPrompt_WhenHostDaemonIsAvailableButDirectAccessIsMissing_ReturnsTrue()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["APPIMAGE"] = "/tmp/CrossMacro.AppImage",
            ["FLATPAK_ID"] = null,
            ["XDG_SESSION_TYPE"] = "wayland",
        };
        var service = CreateService(
            env,
            InputProviderMode.Daemon,
            canReadInputEvents: false,
            effectiveUid: 1000,
            (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        Assert.True(service.ShouldPrompt());
    }

    private static AppImageQuickSetupService CreateService(
        IReadOnlyDictionary<string, string?> env,
        InputProviderMode mode,
        bool canReadInputEvents,
        uint? effectiveUid,
        Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> runProcess)
    {
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => effectiveUid, () => "0 0 4294967295", _ => ValueTask.FromResult<uint?>(null)),
            runProcess);

        return new AppImageQuickSetupService(
            new FakeCapabilityDetector(mode, canReadInputEvents),
            key => env.TryGetValue(key, out var value) ? value : null,
            executor,
            QuickSetupReadinessTests.CreateLauncher());
    }

    private sealed class FakeCapabilityDetector(InputProviderMode mode, bool canReadInputEvents) : ILinuxInputCapabilityDetector
    {
        private readonly InputProviderMode _mode = mode;

        public bool CanConnectToDaemon => false;
        public bool CanUseDirectUInput => _mode is InputProviderMode.Legacy;
        public bool CanReadInputEvents { get; } = canReadInputEvents;
        public int InvalidateCallCount { get; private set; }

        public LinuxInputCapabilitySnapshot GetSnapshot()
            => new(
                ResolvedSocketPath: null,
                DaemonSocketExists: false,
                DaemonHandshakeSucceeded: false,
                DaemonHandshakeTimedOut: false,
                CanUseDirectUInput: _mode is InputProviderMode.Legacy,
                CanReadInputEvents: CanReadInputEvents);

        public InputProviderMode DetermineMode() => _mode;

        public void InvalidateDirectInputCache() { }

        public void InvalidateCache()
        {
            InvalidateCallCount++;
        }
    }
}
