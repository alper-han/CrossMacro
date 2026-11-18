
namespace CrossMacro.Infrastructure.Tests.Services.Shell;

public sealed class ShellCommandRunnerTests
{
    private static readonly string LargeStandardInput = new('x', 1_048_576);

    [Fact]
    public async Task RunAsync_WhenCommandIsEmpty_ThrowsArgumentException()
    {
        var runner = new ShellCommandRunner();

        _ = await Assert.ThrowsAsync<ArgumentException>(() =>
            runner.RunAsync(new ShellCommandRequest(" "), timeout: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_WhenOutputLimitIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var runner = new ShellCommandRunner();

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            runner.RunAsync(new ShellCommandRequest("true", OutputLimitChars: -1), timeout: null, TestContext.Current.CancellationToken));
    }

    [ProcessIntegrationFact]
    public async Task RunAsync_WhenCommandWritesOutput_CapturesStdout()
    {
        var runner = new ShellCommandRunner();

        var result = await runner.RunAsync(new ShellCommandRequest("printf hello"), timeout: null, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("hello", result.StandardOutput);
    }

    [ProcessIntegrationFact]
    public async Task RunAsync_WhenCommandExitsNonZero_ReturnsExitCodeAndStderr()
    {
        var runner = new ShellCommandRunner();

        var result = await runner.RunAsync(new ShellCommandRequest("printf failure >&2; exit 7"), timeout: null, TestContext.Current.CancellationToken);

        Assert.Equal(7, result.ExitCode);
        Assert.Equal("failure", result.StandardError);
    }

    [ProcessIntegrationFact(Timeout = 20000)]
    public async Task RunAsync_WhenTimeoutExpires_KillsProcessTreeAndThrowsTimeout()
    {
        using var probe = new ChildProcessProbe();
        var runner = new ShellCommandRunner();

        _ = await Assert.ThrowsAsync<ShellCommandTimeoutException>(() =>
            runner.RunAsync(new ShellCommandRequest(probe.LongLivedChildCommand), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));

        var childPid = await probe.WaitForChildStartedAsync(TestContext.Current.CancellationToken);
        await ChildProcessProbe.WaitForChildGoneAsync(childPid, TestContext.Current.CancellationToken);
    }

    [ProcessIntegrationFact(Timeout = 20000)]
    public async Task RunAsync_WhenTimeoutExpires_ExceptionDoesNotCarryTheCommandText()
    {
        const string secret = "hunter2-token";
        var runner = new ShellCommandRunner();

        var exception = await Assert.ThrowsAsync<ShellCommandTimeoutException>(() =>
            runner.RunAsync(new ShellCommandRequest($"sleep 5 # {secret}"), TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken));

        Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ShellCommandRequest_ToString_DoesNotPrintCommandOrStandardInput()
    {
        var request = new ShellCommandRequest("curl -H 'Authorization: hunter2'", "password=hunter2");

        var text = request.ToString();

        Assert.DoesNotContain("hunter2", text, StringComparison.Ordinal);
        Assert.DoesNotContain("curl", text, StringComparison.Ordinal);
    }

    [ProcessIntegrationFact(Timeout = 20000)]
    public async Task RunAsync_WhenCancelled_KillsProcessTreeAndPropagatesCancellation()
    {
        using var probe = new ChildProcessProbe();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runner = new ShellCommandRunner();

        var run = runner.RunAsync(new ShellCommandRequest(probe.LongLivedChildCommand), timeout: null, cancellation.Token);
        var childPid = await probe.WaitForChildStartedAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        await ChildProcessProbe.WaitForChildGoneAsync(childPid, TestContext.Current.CancellationToken);
    }

    [ProcessIntegrationFact]
    public async Task RunAsync_WhenStandardInputIsProvided_WritesInputToProcess()
    {
        var runner = new ShellCommandRunner();

        var result = await runner.RunAsync(new ShellCommandRequest("cat", StandardInput: "hello stdin"), timeout: null, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("hello stdin", result.StandardOutput);
    }

    [ProcessIntegrationFact]
    public async Task RunAsync_WhenStandardInputConsumerExitsEarly_ReturnsExitResult()
    {
        var runner = new ShellCommandRunner();

        var result = await runner.RunAsync(
            new ShellCommandRequest("sleep 0.1; exit 0", StandardInput: LargeStandardInput),
            timeout: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [ProcessIntegrationFact]
    public async Task RunAsync_WhenStandardInputConsumerExitsEarlyWithNonZeroExit_ReturnsExitResult()
    {
        var runner = new ShellCommandRunner();

        var result = await runner.RunAsync(
            new ShellCommandRequest("sleep 0.1; printf failure >&2; exit 7", StandardInput: LargeStandardInput),
            timeout: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(7, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal("failure", result.StandardError);
    }

    [ProcessIntegrationFact]
    public async Task RunAsync_WhenOutputExceedsLimit_CapsOutputButStillCompletes()
    {
        var runner = new ShellCommandRunner();

        var result = await runner.RunAsync(
            new ShellCommandRequest("printf 123456789", OutputLimitChars: 4),
            timeout: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("1234", result.StandardOutput);
    }

    [ProcessIntegrationFact]
    public async Task RunAsync_WhenBothStreamsExceedLimit_CapsBothStreams()
    {
        var runner = new ShellCommandRunner();

        var result = await runner.RunAsync(
            new ShellCommandRequest("printf stdout-value; printf stderr-value >&2", OutputLimitChars: 6),
            timeout: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("stdout", result.StandardOutput);
        Assert.Equal("stderr", result.StandardError);
    }
}
