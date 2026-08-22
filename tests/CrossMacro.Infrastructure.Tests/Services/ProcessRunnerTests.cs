namespace CrossMacro.Infrastructure.Tests.Services;


public sealed class ProcessRunnerTests
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
        var readyMarker = $"{marker}-ready";
        await using var cleanup = new TempFileCleanup(marker);
        await using var readyCleanup = new TempFileCleanup(readyMarker);
        using var cancellation = new CancellationTokenSource();
        var runner = new ProcessRunner();

        var command = runner.RunCommandAsync(
            "sh",
            ["-c", $"touch {readyMarker}; sleep 1; touch {marker}"],
            string.Empty,
            cancellation.Token);
        await WaitForFileAsync(readyMarker, TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            command);

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

    [Fact]
    public async Task WriteClipboardInputAndCloseAsync_WhenCommandExitsNonZero_ThrowsInvalidOperationException()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var runner = new ProcessRunner();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.WriteClipboardInputAndCloseAsync("sh", ["-c", "cat >/dev/null; exit 11"], "hello"));

        Assert.Contains("sh", ex.Message, StringComparison.Ordinal);
        Assert.Contains("exited with code 11", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteClipboardInputAndCloseAsync_WhenSuccessfulChildKeepsStderrOpen_DoesNotWaitForStderr()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var runner = new ProcessRunner();
        var startedAt = Stopwatch.GetTimestamp();

        await runner.WriteClipboardInputAndCloseAsync(
            "sh",
            ["-c", "cat >/dev/null; (sleep 1) >&2 &"],
            "hello");

        Assert.True(Stopwatch.GetElapsedTime(startedAt) < TimeSpan.FromMilliseconds(400));
    }

    [Fact(Timeout = 5000)]
    public async Task WriteClipboardInputAndCloseAsync_WhenCommandWritesLargeStderrAndExitsNonZero_ThrowsWithStderr()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var runner = new ProcessRunner();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.WriteClipboardInputAndCloseAsync(
                "sh",
                ["-c", "cat >/dev/null; printf failure >&2; head -c 1048576 /dev/zero >&2; exit 17"],
                "hello"));

        Assert.Contains("exited with code 17", ex.Message, StringComparison.Ordinal);
        Assert.Contains("failure", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteClipboardInputAndCloseAsync_WhenInputIsBytes_WritesBytesUnchanged()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var marker = $"/tmp/crossmacro-process-runner-bytes-{Guid.NewGuid():N}";
        await using var cleanup = new TempFileCleanup(marker);
        byte[] input = [0x00, 0x01, 0xFF, 0x41, 0x0A];
        var runner = new ProcessRunner();

        await runner.WriteClipboardInputAndCloseAsync("sh", ["-c", $"cat > {marker}"], input);

        Assert.Equal(input, await File.ReadAllBytesAsync(marker));
    }

    [Fact(Timeout = 5000)]
    public async Task WriteClipboardInputAndCloseAsync_WhenCommandKeepsRunningAfterInput_ReturnsAfterSafetyTimeout()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var runner = new ProcessRunner();

        await runner.WriteClipboardInputAndCloseAsync(
            "sh",
            ["-c", "read _; sleep 10"],
            "hello\n",
            CancellationToken.None);
    }

    private sealed class TempFileCleanup(string path) : IAsyncDisposable
    {
        private readonly string _path = path;

        public ValueTask DisposeAsync()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            return ValueTask.CompletedTask;
        }
    }

    private static async Task WaitForFileAsync(string path, TimeSpan timeout)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        while (!File.Exists(path))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeoutCts.Token);
        }
    }
}
