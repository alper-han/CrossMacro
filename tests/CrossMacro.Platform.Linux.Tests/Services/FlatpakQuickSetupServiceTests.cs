
namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class FlatpakQuickSetupServiceTests
{
    [LinuxFact]
    public void IsApplicable_WhenFlatpakWayland_ShouldReturnTrue()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["FLATPAK_ID"] = "io.github.alper_han.crossmacro",
            ["XDG_SESSION_TYPE"] = "wayland",
        };

        var service = CreateService(
            env,
            effectiveUid: 1000,
            (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        var result = service.IsApplicable();

        Assert.True(result);
    }

    [Fact]
    public void IsApplicable_WhenNotFlatpak_ShouldReturnFalse()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["FLATPAK_ID"] = null,
            ["XDG_SESSION_TYPE"] = "wayland",
        };

        var service = CreateService(
            env,
            effectiveUid: 1000,
            (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        var result = service.IsApplicable();

        Assert.False(result);
    }

    [Fact]
    public async Task RunAsync_WhenIdentityUnavailable_ShouldFailWithoutRunningCommand()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["FLATPAK_ID"] = "io.github.alper_han.crossmacro",
            ["XDG_SESSION_TYPE"] = "wayland",
        };

        var commandWasRun = false;
        var service = CreateService(
            env,
            effectiveUid: null,
            (_, _) =>
            {
                commandWasRun = true;
                return Task.FromResult((0, string.Empty, string.Empty));
            });

        var result = await service.RunAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(commandWasRun);
    }


    private static FlatpakQuickSetupService CreateService(
        IReadOnlyDictionary<string, string?> env,
        uint? effectiveUid,
        Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> runProcess)
    {
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => effectiveUid, () => "0 0 4294967295", _ => ValueTask.FromResult<uint?>(null)),
            runProcess);

        return new FlatpakQuickSetupService(
            key => env.TryGetValue(key, out var value) ? value : null,
            executor,
            QuickSetupReadinessTests.CreateLauncher(),
            QuickSetupReadinessTests.CreateSnapshotProvider());
    }
}
