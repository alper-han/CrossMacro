
namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class FlatpakQuickSetupServiceTests
{
    [LinuxFact]
    public void IsApplicable_WhenFlatpakWayland_ShouldReturnTrue()
    {
        var service = CreateService(Environment("io.github.alper_han.crossmacro", "wayland"), effectiveUid: 1000);

        Assert.True(service.IsApplicable());
    }

    [Fact]
    public void IsApplicable_WhenNotFlatpak_ShouldReturnFalse()
    {
        var service = CreateService(Environment(flatpakId: null, "wayland"), effectiveUid: 1000);

        Assert.False(service.IsApplicable());
    }

    [Fact]
    public void IsApplicable_WhenFlatpakX11_ShouldReturnFalse()
    {
        var service = CreateService(Environment("io.github.alper_han.crossmacro", "x11"), effectiveUid: 1000);

        Assert.False(service.IsApplicable());
    }

    [Fact]
    public async Task RunAsync_WhenIdentityUnavailable_ShouldFailWithoutRunningCommand()
    {
        var commandWasRun = false;
        var service = CreateService(
            Environment("io.github.alper_han.crossmacro", "wayland"),
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

    private static LinuxEnvironmentSnapshot Environment(string? flatpakId, string sessionType) =>
        new(
            FlatpakId: flatpakId,
            AppImage: null,
            SessionType: sessionType,
            WaylandDisplay: null,
            Display: null,
            CurrentDesktop: null,
            GdmSession: null,
            HyprlandInstanceSignature: null,
            RuntimeDir: null,
            WayfireSocket: null,
            SwaySocket: null,
            WindowButtons: null);

    private static FlatpakQuickSetupService CreateService(
        LinuxEnvironmentSnapshot environment,
        uint? effectiveUid,
        Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>>? runProcess = null)
    {
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => effectiveUid, () => "0 0 4294967295", _ => ValueTask.FromResult<uint?>(null)),
            runProcess ?? ((_, _) => Task.FromResult((0, string.Empty, string.Empty))));

        return new FlatpakQuickSetupService(
            environment,
            executor,
            QuickSetupReadinessTests.CreateLauncher(),
            QuickSetupReadinessTests.CreateSnapshotProvider());
    }
}
