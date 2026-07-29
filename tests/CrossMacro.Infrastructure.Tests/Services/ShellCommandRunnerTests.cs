
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class ShellCommandRunnerTests
{
    private static readonly string LargeStandardInput = new('x', 1_048_576);

    [Fact]
    public async Task RunAsync_WhenCommandWritesOutput_CapturesStdout()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var runner = new ShellCommandRunner();

        var result = await runner.RunAsync(new ShellCommandRequest("printf hello"), timeout: null);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("hello", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_WhenCommandExitsNonZero_ReturnsExitCodeAndStderr()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var runner = new ShellCommandRunner();

        var result = await runner.RunAsync(new ShellCommandRequest("printf failure >&2; exit 7"), timeout: null);

        Assert.Equal(7, result.ExitCode);
        Assert.Equal("failure", result.StandardError);
    }

    [Fact(Timeout = 5000)]
    public async Task RunAsync_WhenTimeoutExpires_KillsProcessTreeAndThrowsTimeout()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var marker = $"/tmp/crossmacro-shell-runner-timeout-{Guid.NewGuid():N}";
        await using var cleanup = new TempFileCleanup(marker);
        var runner = new ShellCommandRunner();

        _ = await Assert.ThrowsAsync<ShellCommandTimeoutException>(() =>
            runner.RunAsync(new ShellCommandRequest($"sleep 1; touch {marker}"), TimeSpan.FromMilliseconds(100)));

        await Task.Delay(TimeSpan.FromMilliseconds(1500));
        Assert.False(File.Exists(marker));
    }

    [Fact(Timeout = 5000)]
    public async Task RunAsync_WhenCancelled_KillsProcessTreeAndPropagatesCancellation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var marker = $"/tmp/crossmacro-shell-runner-cancel-{Guid.NewGuid():N}";
        await using var cleanup = new TempFileCleanup(marker);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var runner = new ShellCommandRunner();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(new ShellCommandRequest($"sleep 1; touch {marker}"), timeout: null, cancellation.Token));

        await Task.Delay(TimeSpan.FromMilliseconds(1500));
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task RunAsync_WhenStandardInputIsProvided_WritesInputToProcess()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var runner = new ShellCommandRunner();

        var result = await runner.RunAsync(new ShellCommandRequest("cat", StandardInput: "hello stdin"), timeout: null);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("hello stdin", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_WhenStandardInputConsumerExitsEarly_ReturnsExitResult()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var runner = new ShellCommandRunner();

        var result = await runner.RunAsync(
            new ShellCommandRequest("sleep 0.1; exit 0", StandardInput: LargeStandardInput),
            timeout: null);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task RunAsync_WhenStandardInputConsumerExitsEarlyWithNonZeroExit_ReturnsExitResult()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var runner = new ShellCommandRunner();

        var result = await runner.RunAsync(
            new ShellCommandRequest("sleep 0.1; printf failure >&2; exit 7", StandardInput: LargeStandardInput),
            timeout: null);

        Assert.Equal(7, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal("failure", result.StandardError);
    }

    [Fact]
    public async Task RunAsync_WhenOutputExceedsLimit_CapsOutputButStillCompletes()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var runner = new ShellCommandRunner();

        var result = await runner.RunAsync(
            new ShellCommandRequest("printf 123456789", OutputLimitChars: 4),
            timeout: null);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("1234", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_WhenBothStreamsExceedLimit_CapsBothStreams()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var runner = new ShellCommandRunner();

        var result = await runner.RunAsync(
            new ShellCommandRequest("printf stdout-value; printf stderr-value >&2", OutputLimitChars: 6),
            timeout: null);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("stdout", result.StandardOutput);
        Assert.Equal("stderr", result.StandardError);
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
}
