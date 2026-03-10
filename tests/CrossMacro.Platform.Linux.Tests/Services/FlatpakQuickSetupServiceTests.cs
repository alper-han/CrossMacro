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

public sealed class FlatpakQuickSetupServiceTests
{
    [LinuxFact]
    public void IsApplicable_WhenFlatpakWayland_ShouldReturnTrue()
    {
        var env = new Dictionary<string, string?>
        {
            ["FLATPAK_ID"] = "io.github.alper_han.crossmacro",
            ["XDG_SESSION_TYPE"] = "wayland"
        };

        var service = CreateService(
            env,
            userName: "alice",
            effectiveUid: 1000,
            (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        var result = service.IsApplicable();

        Assert.True(result);
    }

    [Fact]
    public void IsApplicable_WhenNotFlatpak_ShouldReturnFalse()
    {
        var env = new Dictionary<string, string?>
        {
            ["FLATPAK_ID"] = null,
            ["XDG_SESSION_TYPE"] = "wayland"
        };

        var service = CreateService(
            env,
            userName: "alice",
            effectiveUid: 1000,
            (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        var result = service.IsApplicable();

        Assert.False(result);
    }

    [Fact]
    public async Task RunAsync_WhenIdentityUnavailable_ShouldFailWithoutRunningCommand()
    {
        var env = new Dictionary<string, string?>
        {
            ["FLATPAK_ID"] = "io.github.alper_han.crossmacro",
            ["XDG_SESSION_TYPE"] = "wayland"
        };

        var commandWasRun = false;
        var service = CreateService(
            env,
            userName: " ",
            effectiveUid: null,
            (_, _) =>
            {
                commandWasRun = true;
                return Task.FromResult((0, string.Empty, string.Empty));
            });

        var result = await service.RunAsync();

        Assert.False(result.Success);
        Assert.False(commandWasRun);
    }

    [Fact]
    public async Task RunAsync_WhenUidAvailable_ShouldUseUidForAclIdentity()
    {
        var env = new Dictionary<string, string?>
        {
            ["FLATPAK_ID"] = "io.github.alper_han.crossmacro",
            ["XDG_SESSION_TYPE"] = "wayland"
        };

        ProcessStartInfo? capturedStartInfo = null;
        var service = CreateService(
            env,
            userName: "User.Name",
            effectiveUid: 1042,
            (startInfo, _) =>
            {
                capturedStartInfo = startInfo;
                return Task.FromResult((0, "ok", string.Empty));
            });

        var result = await service.RunAsync();

        Assert.True(result.Success);
        Assert.NotNull(capturedStartInfo);
        Assert.Equal("flatpak-spawn", capturedStartInfo!.FileName);
        Assert.Contains("--host", capturedStartInfo.ArgumentList);
        Assert.Contains("pkexec", capturedStartInfo.ArgumentList);
        Assert.Equal("1042", capturedStartInfo.ArgumentList[^1]);
    }

    [Fact]
    public async Task RunAsync_WhenUidUnavailable_ShouldAcceptEnterpriseStyleUserName()
    {
        var env = new Dictionary<string, string?>
        {
            ["FLATPAK_ID"] = "io.github.alper_han.crossmacro",
            ["XDG_SESSION_TYPE"] = "wayland"
        };

        ProcessStartInfo? capturedStartInfo = null;
        var service = CreateService(
            env,
            userName: "John.Doe",
            effectiveUid: null,
            (startInfo, _) =>
            {
                capturedStartInfo = startInfo;
                return Task.FromResult((0, "ok", string.Empty));
            });

        var result = await service.RunAsync();

        Assert.True(result.Success);
        Assert.NotNull(capturedStartInfo);
        Assert.Equal("John.Doe", capturedStartInfo!.ArgumentList[^1]);
    }

    [Fact]
    public async Task RunAsync_WhenCommandFails_ShouldReturnErrorMessage()
    {
        var env = new Dictionary<string, string?>
        {
            ["FLATPAK_ID"] = "io.github.alper_han.crossmacro",
            ["XDG_SESSION_TYPE"] = "wayland"
        };

        var service = CreateService(
            env,
            userName: "alice",
            effectiveUid: 1000,
            (_, _) => Task.FromResult((22, string.Empty, "setfacl is missing on host")));

        var result = await service.RunAsync();

        Assert.False(result.Success);
        Assert.Contains("setfacl is missing on host", result.Message, StringComparison.Ordinal);
    }

    private static FlatpakQuickSetupService CreateService(
        IReadOnlyDictionary<string, string?> env,
        string userName,
        uint? effectiveUid,
        Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> runProcess)
    {
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => userName, () => effectiveUid),
            new LinuxQuickSetupScriptBuilder(),
            runProcess);

        return new FlatpakQuickSetupService(
            key => env.TryGetValue(key, out var value) ? value : null,
            executor,
            new FlatpakHostCommandLauncher(_ => true, _ => true));
    }
}
