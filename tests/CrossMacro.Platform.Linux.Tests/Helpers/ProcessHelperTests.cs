using CrossMacro.Platform.Linux.Helpers;

namespace CrossMacro.Platform.Linux.Tests.Helpers;

public sealed class ProcessHelperTests
{
    [Fact]
    public async Task ExecuteCommandAsync_ReturnsNull_WhenOutputNeverCompletes()
    {
        var execution = ProcessHelper.ExecuteCommandAsync("/bin/sh", ["-c", "sleep 8"], TestContext.Current.CancellationToken);
        try
        {
            Assert.Null(await execution.WaitAsync(TimeSpan.FromSeconds(4), TimeProvider.System, TestContext.Current.CancellationToken));
        }
        finally
        {
            await execution;
        }
    }

    [Fact]
    public async Task ExecuteCommandAsync_ReturnsNull_WhenOutputClosesButProcessKeepsRunning()
    {
        var execution = ProcessHelper.ExecuteCommandAsync("/bin/sh", ["-c", "exec 1>&-; sleep 8"], TestContext.Current.CancellationToken);
        try
        {
            Assert.Null(await execution.WaitAsync(TimeSpan.FromSeconds(4), TimeProvider.System, TestContext.Current.CancellationToken));
        }
        finally
        {
            await execution;
        }
    }

    [Fact]
    public async Task ExecuteCommandAsync_ReturnsTrimmedOutput_OnlyForSuccessfulCommands()
    {
        Assert.Equal("tr", await ProcessHelper.ExecuteCommandAsync("/bin/sh", ["-c", "printf '  tr  '"], TestContext.Current.CancellationToken));
        Assert.Null(await ProcessHelper.ExecuteCommandAsync("/bin/sh", ["-c", "printf tr; exit 1"], TestContext.Current.CancellationToken));
        Assert.Null(await ProcessHelper.ExecuteCommandAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), [], TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteCommandAsync_StopsProcessTree_OnTimeoutOrCancellation(bool cancelExplicitly)
    {
        var pidFile = Path.Combine(Path.GetTempPath(), $"crossmacro-process-{Guid.NewGuid():N}");
        using var cancellation = new CancellationTokenSource();
        var execution = ProcessHelper.ExecuteCommandAsync(
            "/bin/sh", ["-c", $"sleep 8 & child=$!; echo $$ $child > '{pidFile}'; wait"], cancellation.Token);
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
                Assert.Null(await execution.WaitAsync(TimeSpan.FromSeconds(4), TimeProvider.System, TestContext.Current.CancellationToken));
            }

            foreach (var pid in pids)
            {
                try
                {
                    using var process = Process.GetProcessById(pid);
                    await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
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
