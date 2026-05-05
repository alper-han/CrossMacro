namespace CrossMacro.Infrastructure.Tests.Services;

using System;
using System.Threading;
using CrossMacro.Infrastructure.Services;

public class ProcessRunnerTests
{
    [Fact]
    public async Task CheckCommandAsync_WhenCommandDoesNotExist_ReturnsFalse()
    {
        var runner = new ProcessRunner();
        var fakeCommand = $"crossmacro_nonexistent_{Guid.NewGuid():N}";

        var exists = await runner.CheckCommandAsync(fakeCommand);

        Assert.False(exists);
    }

    [Fact(Timeout = 5000)]
    public async Task RunCommandAsync_WhenCancelled_KillsChildProcessAndThrows()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var marker = $"/tmp/crossmacro-process-runner-{Guid.NewGuid():N}";
        await using var cleanup = new TempFileCleanup(marker);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var runner = new ProcessRunner();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunCommandAsync("sh", ["-c", $"sleep 1; touch {marker}"], string.Empty, cancellation.Token));

        await Task.Delay(TimeSpan.FromMilliseconds(1500));
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task RunCommandAsync_WhenCommandExitsNonZero_ThrowsInvalidOperationException()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var runner = new ProcessRunner();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunCommandAsync("sh", ["-c", "printf failure >&2; exit 7"], string.Empty));

        Assert.Contains("exited with code 7", ex.Message, StringComparison.Ordinal);
        Assert.Contains("failure", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadCommandAsync_WhenCommandExitsNonZero_ThrowsInvalidOperationException()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var runner = new ProcessRunner();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.ReadCommandAsync("sh", ["-c", "printf failure >&2; exit 9"]));

        Assert.Contains("exited with code 9", ex.Message, StringComparison.Ordinal);
        Assert.Contains("failure", ex.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 5000)]
    public async Task WriteInputAndCloseAsync_WhenCommandKeepsRunningAfterInput_ReturnsAfterClosingStdin()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var runner = new ProcessRunner();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await runner.WriteInputAndCloseAsync(
            "sh",
            ["-c", "read _; sleep 10"],
            "hello\n",
            timeout.Token);
    }

    private sealed class TempFileCleanup : IAsyncDisposable
    {
        private readonly string _path;

        public TempFileCleanup(string path)
        {
            _path = path;
        }

        public ValueTask DisposeAsync()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            return ValueTask.CompletedTask;
        }
    }
}
