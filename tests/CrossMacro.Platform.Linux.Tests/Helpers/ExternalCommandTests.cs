using CrossMacro.Platform.Linux.Helpers;

namespace CrossMacro.Platform.Linux.Tests.Helpers;

public sealed class ExternalCommandTests
{
    [LinuxFact]
    public async Task RunAsync_ReturnsExitCodeAndBothStreams()
    {
        var result = await ExternalCommand.RunAsync(
            ExternalCommand.CreateStartInfo("/bin/sh", ["-c", "printf out; printf err >&2; exit 3"]),
            timeout: null,
            TestContext.Current.CancellationToken);

        Assert.Equal((3, "out", "err"), result);
    }

    [LinuxFact]
    public async Task RunAsync_PassesEveryArgumentAsOneValue()
    {
        var result = await ExternalCommand.RunAsync(
            ExternalCommand.CreateStartInfo("/bin/sh", ["-c", "printf '%s|' \"$1\" \"$2\"", "probe", "two words", "$HOME; echo injected"]),
            timeout: null,
            TestContext.Current.CancellationToken);

        Assert.Equal("two words|$HOME; echo injected|", result.StdOut);
    }

    [LinuxFact]
    public async Task RunAsync_WhenDeadlinePasses_KillsTheProcessAndThrowsTimeout()
    {
        var startInfo = ExternalCommand.CreateStartInfo("/bin/sh", ["-c", "sleep 30"]);

        _ = await Assert.ThrowsAsync<TimeoutException>(() => ExternalCommand.RunAsync(
            startInfo,
            TimeSpan.FromMilliseconds(200),
            TestContext.Current.CancellationToken));
    }

    [LinuxFact]
    public async Task RunAsync_WhenCancelled_ThrowsCancellationInsteadOfTimeout()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = ExternalCommand.RunAsync(
            ExternalCommand.CreateStartInfo("/bin/sh", ["-c", "sleep 30"]),
            TimeSpan.FromSeconds(30),
            cancellation.Token);

        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    }

    [LinuxFact]
    public async Task RunAsync_WhenProgramIsMissing_ThrowsWin32Exception()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        _ = await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() => ExternalCommand.RunAsync(
            ExternalCommand.CreateStartInfo(missing, []),
            timeout: null,
            TestContext.Current.CancellationToken));
    }
}
