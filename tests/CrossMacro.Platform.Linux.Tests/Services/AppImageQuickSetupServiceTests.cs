
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
            userName: "alice",
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
            userName: "alice",
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
            userName: "alice",
            effectiveUid: 1000,
            (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        Assert.True(service.ShouldPrompt());
    }


    [Fact]
    public async Task RunAsync_WhenNoPrivilegeCommandIsAvailable_ShouldFailWithoutRunningCommand()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["APPIMAGE"] = "/tmp/CrossMacro.AppImage",
            ["FLATPAK_ID"] = null,
            ["XDG_SESSION_TYPE"] = "wayland",
        };

        var commandWasRun = false;
        var service = CreateService(
            env,
            InputProviderMode.None,
            canReadInputEvents: false,
            userName: "alice",
            effectiveUid: 1000,
            (_, _) =>
            {
                commandWasRun = true;
                return Task.FromResult((0, string.Empty, string.Empty));
            },
            commandExists: (_, _) => ValueTask.FromResult(false));

        var result = await service.RunAsync();

        Assert.False(result.Success);
        Assert.False(commandWasRun);
        Assert.Contains("Neither pkexec nor systemd run0", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenUidAvailable_ShouldUseUidAndInvalidateCacheOnSuccess()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["APPIMAGE"] = "/tmp/CrossMacro.AppImage",
            ["FLATPAK_ID"] = null,
            ["XDG_SESSION_TYPE"] = "wayland",
        };

        var detector = new FakeCapabilityDetector(InputProviderMode.None, canReadInputEvents: false);
        ProcessStartInfo? capturedStartInfo = null;
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => "alice", () => 1042),
            (startInfo, _) =>
            {
                capturedStartInfo = startInfo;
                return Task.FromResult((0, "Applied session ACLs for 1042: uinput=1, input-events=3.\n", string.Empty));
            });

        var service = new AppImageQuickSetupService(
            detector,
            key => env.TryGetValue(key, out var value) ? value : null,
            executor,
            new DirectPolkitHostCommandLauncher(
                (_, _) => ValueTask.FromResult(true),
                _ => ValueTask.FromResult(true)));

        var result = await service.RunAsync();

        Assert.True(result.Success);
        Assert.Contains("Applied session ACLs for 1042: uinput=1, input-events=3.", result.Message, StringComparison.Ordinal);
        Assert.Equal(1, detector.InvalidateCallCount);
        Assert.NotNull(capturedStartInfo);
        Assert.Equal("pkexec", capturedStartInfo!.FileName);
        Assert.Equal("1042", capturedStartInfo.ArgumentList[^1]);
        Assert.Contains("uinput_ok=0", capturedStartInfo.ArgumentList[2], StringComparison.Ordinal);
        Assert.Contains("event_ok=0", capturedStartInfo.ArgumentList[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenCommandFails_ShouldReturnErrorMessage()
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
            userName: "alice",
            effectiveUid: 1000,
            (_, _) => Task.FromResult((22, string.Empty, "setfacl is missing on host")));

        var result = await service.RunAsync();

        Assert.False(result.Success);
        Assert.Contains("setfacl is missing on host", result.Message, StringComparison.Ordinal);
    }

    private static AppImageQuickSetupService CreateService(
        IReadOnlyDictionary<string, string?> env,
        InputProviderMode mode,
        bool canReadInputEvents,
        string userName,
        uint? effectiveUid,
        Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> runProcess,
        Func<string, CancellationToken, ValueTask<bool>>? commandExists = null)
    {
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => userName, () => effectiveUid),
            runProcess);

        return new AppImageQuickSetupService(
            new FakeCapabilityDetector(mode, canReadInputEvents),
            key => env.TryGetValue(key, out var value) ? value : null,
            executor,
            new DirectPolkitHostCommandLauncher(
                commandExists ?? ((_, _) => ValueTask.FromResult(true)),
                _ => ValueTask.FromResult(true)));
    }

    private sealed class FakeCapabilityDetector(InputProviderMode mode, bool canReadInputEvents) : ILinuxInputCapabilityDetector
    {
        private readonly InputProviderMode _mode = mode;

        public bool CanConnectToDaemon => false;
        public bool CanUseDirectUInput => false;
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

        public void InvalidateCache()
        {
            InvalidateCallCount++;
        }
    }
}
