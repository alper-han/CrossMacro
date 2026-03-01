using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.TestInfrastructure;
using CrossMacro.UI.Services;
using FluentAssertions;

namespace CrossMacro.UI.Tests.Services;

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

        result.Should().BeTrue();
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

        result.Should().BeFalse();
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

        result.Success.Should().BeFalse();
        commandWasRun.Should().BeFalse();
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

        result.Success.Should().BeTrue();
        capturedStartInfo.Should().NotBeNull();
        capturedStartInfo!.FileName.Should().Be("flatpak-spawn");
        capturedStartInfo.ArgumentList.Should().Contain("--host");
        capturedStartInfo.ArgumentList.Should().Contain("pkexec");
        capturedStartInfo.ArgumentList[^1].Should().Be("1042");
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

        result.Success.Should().BeTrue();
        capturedStartInfo.Should().NotBeNull();
        capturedStartInfo!.ArgumentList[^1].Should().Be("John.Doe");
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

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("setfacl is missing on host");
    }

    private static FlatpakQuickSetupService CreateService(
        IReadOnlyDictionary<string, string?> env,
        string userName,
        uint? effectiveUid,
        Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> runProcess)
    {
        return new FlatpakQuickSetupService(
            key => env.TryGetValue(key, out var value) ? value : null,
            () => userName,
            () => effectiveUid,
            runProcess);
    }
}
