using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Services.QuickSetup;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class AppImageQuickSetupServiceTests
{
    [LinuxFact]
    public void IsApplicable_WhenAppImageWayland_ShouldReturnTrue()
    {
        var env = new Dictionary<string, string?>
        {
            ["APPIMAGE"] = "/tmp/CrossMacro.AppImage",
            ["FLATPAK_ID"] = null,
            ["XDG_SESSION_TYPE"] = "wayland"
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
        var env = new Dictionary<string, string?>
        {
            ["APPIMAGE"] = "/tmp/CrossMacro.AppImage",
            ["FLATPAK_ID"] = null,
            ["XDG_SESSION_TYPE"] = "wayland"
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
    public void ShouldPrompt_WhenLegacyModeButInputEventsAreUnreadable_ShouldReturnTrue()
    {
        var env = new Dictionary<string, string?>
        {
            ["APPIMAGE"] = "/tmp/CrossMacro.AppImage",
            ["FLATPAK_ID"] = null,
            ["XDG_SESSION_TYPE"] = "wayland"
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
    public async Task RunAsync_WhenPkexecMissing_ShouldFailWithoutRunningCommand()
    {
        var env = new Dictionary<string, string?>
        {
            ["APPIMAGE"] = "/tmp/CrossMacro.AppImage",
            ["FLATPAK_ID"] = null,
            ["XDG_SESSION_TYPE"] = "wayland"
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
            commandExists: _ => false);

        var result = await service.RunAsync();

        Assert.False(result.Success);
        Assert.False(commandWasRun);
        Assert.Contains("pkexec is missing", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenUidAvailable_ShouldUseUidAndInvalidateCacheOnSuccess()
    {
        var env = new Dictionary<string, string?>
        {
            ["APPIMAGE"] = "/tmp/CrossMacro.AppImage",
            ["FLATPAK_ID"] = null,
            ["XDG_SESSION_TYPE"] = "wayland"
        };

        var detector = new FakeCapabilityDetector(InputProviderMode.None, canReadInputEvents: false);
        ProcessStartInfo? capturedStartInfo = null;
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => "alice", () => 1042),
            new LinuxQuickSetupScriptBuilder(),
            (startInfo, _) =>
            {
                capturedStartInfo = startInfo;
                return Task.FromResult((0, "ok", string.Empty));
            });

        var service = new AppImageQuickSetupService(
            detector,
            key => env.TryGetValue(key, out var value) ? value : null,
            executor,
            new DirectPkexecHostCommandLauncher(_ => true));

        var result = await service.RunAsync();

        Assert.True(result.Success);
        Assert.Equal(1, detector.InvalidateCallCount);
        Assert.NotNull(capturedStartInfo);
        Assert.Equal("pkexec", capturedStartInfo!.FileName);
        Assert.Equal("1042", capturedStartInfo.ArgumentList[^1]);
    }

    [Fact]
    public async Task RunAsync_WhenCommandFails_ShouldReturnErrorMessage()
    {
        var env = new Dictionary<string, string?>
        {
            ["APPIMAGE"] = "/tmp/CrossMacro.AppImage",
            ["FLATPAK_ID"] = null,
            ["XDG_SESSION_TYPE"] = "wayland"
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
        Func<string, bool>? commandExists = null)
    {
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => userName, () => effectiveUid),
            new LinuxQuickSetupScriptBuilder(),
            runProcess);

        return new AppImageQuickSetupService(
            new FakeCapabilityDetector(mode, canReadInputEvents),
            key => env.TryGetValue(key, out var value) ? value : null,
            executor,
            new DirectPkexecHostCommandLauncher(commandExists ?? (_ => true)));
    }

    private sealed class FakeCapabilityDetector : ILinuxInputCapabilityDetector
    {
        private readonly InputProviderMode _mode;
        private readonly bool _canReadInputEvents;

        public FakeCapabilityDetector(InputProviderMode mode, bool canReadInputEvents)
        {
            _mode = mode;
            _canReadInputEvents = canReadInputEvents;
        }

        public bool CanConnectToDaemon => false;
        public bool CanUseDirectUInput => false;
        public bool CanReadInputEvents => _canReadInputEvents;
        public int InvalidateCallCount { get; private set; }

        public InputProviderMode DetermineMode() => _mode;

        public void InvalidateCache()
        {
            InvalidateCallCount++;
        }
    }
}
