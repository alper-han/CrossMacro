using CrossMacro.Platform.Linux.Helpers;

namespace CrossMacro.Platform.Linux.Tests.Helpers;

public sealed class ProcessHelperTests
{
    [LinuxFact]
    public async Task ExecuteCommandAsync_ReturnsNull_WhenOutputNeverCompletes()
    {
        var execution = ProcessHelper.ExecuteCommandAsync("/bin/sh", "-c \"sleep 8\"");
        try
        {
            Assert.Null(await execution.WaitAsync(TimeSpan.FromSeconds(4), TimeProvider.System));
        }
        finally
        {
            await execution;
        }
    }

    [LinuxFact]
    public async Task ExecuteCommandAsync_ReturnsNull_WhenOutputClosesButProcessKeepsRunning()
    {
        var execution = ProcessHelper.ExecuteCommandAsync("/bin/sh", "-c \"exec 1>&-; sleep 8\"");
        try
        {
            Assert.Null(await execution.WaitAsync(TimeSpan.FromSeconds(4), TimeProvider.System));
        }
        finally
        {
            await execution;
        }
    }

    [LinuxFact]
    public async Task ExecuteCommandAsync_ReturnsTrimmedOutput_OnlyForSuccessfulCommands()
    {
        Assert.Equal("tr", await ProcessHelper.ExecuteCommandAsync("/bin/sh", "-c \"printf '  tr  '\""));
        Assert.Null(await ProcessHelper.ExecuteCommandAsync("/bin/sh", "-c \"printf tr; exit 1\""));
        Assert.Null(await ProcessHelper.ExecuteCommandAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }

    [LinuxTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteCommandAsync_StopsProcessTree_OnTimeoutOrCancellation(bool cancelExplicitly)
    {
        var pidFile = Path.Combine(Path.GetTempPath(), $"crossmacro-process-{Guid.NewGuid():N}");
        using var cancellation = new CancellationTokenSource();
        var execution = ProcessHelper.ExecuteCommandAsync(
            "/bin/sh", $"-c \"sleep 8 & child=$!; echo $$ $child > '{pidFile}'; wait\"", cancellation.Token);
        try
        {
            using var startupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(4), TimeProvider.System);
            while (!File.Exists(pidFile) || File.ReadAllText(pidFile).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length is not 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), TimeProvider.System, startupDeadline.Token);
            }

            var pids = File.ReadAllText(pidFile).Trim().Split(' ').Select(int.Parse).ToArray();
            if (cancelExplicitly)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
            }
            else
            {
                Assert.Null(await execution.WaitAsync(TimeSpan.FromSeconds(4), TimeProvider.System));
            }

            foreach (var pid in pids)
            {
                try
                {
                    using var process = Process.GetProcessById(pid);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System);
                    Assert.True(process.HasExited, "Command process is still running");
                }
                catch (ArgumentException)
                {
                    // The process has already been reaped.
                }
            }
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                await execution;
            }
            catch (OperationCanceledException)
            {
                // Expected when caller cancellation wins over the deadline.
            }
            File.Delete(pidFile);
        }
    }

}
