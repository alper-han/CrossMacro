
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class RunScriptShellRuntimeTests
{
    [Fact]
    public async Task ExecuteStepAsync_WhenCommandContainsVariables_ResolvesCommandBeforeExecution()
    {
        var runner = new FakeShellCommandRunner(new ShellCommandResult(0, "", ""));
        var executor = Executor(runner);
        var variables = Vars();
        variables["name"] = "world";

        await executor.ExecuteStepAsync("shell \"printf hello   $name\"", 1, variables, CancellationToken.None);

        runner.Calls.Should().ContainSingle();
        runner.Calls[0].Request.Command.Should().Be("printf hello   world");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCommandFailsThenSucceeds_RetriesWithBackoff()
    {
        var runner = new FakeShellCommandRunner(
            new ShellCommandResult(7, "", "first failure"),
            new ShellCommandResult(0, "", ""));
        var timingService = Substitute.For<IPlaybackTimingService>();
        var pauseToken = Substitute.For<IPlaybackPauseToken>();
        timingService.WaitAsync(Arg.Any<int>(), Arg.Any<IPlaybackPauseToken>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var executor = new RunScriptShellExecutor(runner, timingService, pauseToken);

        await executor.ExecuteStepAsync("shell \"false\" 1 1 0", 3, Vars(), CancellationToken.None);

        runner.Calls.Should().HaveCount(2);
        await timingService.Received(1).WaitAsync(1, pauseToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenQuotedCommandContainsEscapedQuote_UnescapesCommandBeforeExecution()
    {
        var runner = new FakeShellCommandRunner(new ShellCommandResult(0, "", ""));
        var executor = Executor(runner);

        await executor.ExecuteStepAsync("shell \"printf \\\"ok\\\"\"", 2, Vars(), CancellationToken.None);

        runner.Calls.Should().ContainSingle();
        runner.Calls[0].Request.Command.Should().Be("printf \"ok\"");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenQuotedCommandContainsEscapedBackslashes_UnescapesCommandBeforeExecution()
    {
        var runner = new FakeShellCommandRunner(new ShellCommandResult(0, "", ""));
        var executor = Executor(runner);

        await executor.ExecuteStepAsync(@"shell capture ""printf C:\\temp\\"" code stdout stderr", 2, Vars(), CancellationToken.None);

        runner.Calls.Should().ContainSingle();
        runner.Calls[0].Request.Command.Should().Be(@"printf C:\temp\");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCommandContainsEscapedDollar_PreservesShellVariableReference()
    {
        var runner = new FakeShellCommandRunner(new ShellCommandResult(0, "", ""));
        var executor = Executor(runner);

        await executor.ExecuteStepAsync("shell \"printf $$HOME\"", 2, Vars(), CancellationToken.None);

        runner.Calls.Should().ContainSingle();
        runner.Calls[0].Request.Command.Should().Be("printf $HOME");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCaptureModeCompletes_WritesExitStdoutAndStderr()
    {
        var runner = new FakeShellCommandRunner(new ShellCommandResult(0, "out", "err"));
        var executor = Executor(runner);
        var variables = Vars();

        await executor.ExecuteStepAsync("shell capture \"printf ok\" code stdout stderr", 2, variables, CancellationToken.None);

        variables.Should().Contain("code", "0");
        variables.Should().Contain("stdout", "out");
        variables.Should().Contain("stderr", "err");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCaptureModeExitsNonZero_WritesExitCodeAndDoesNotRetry()
    {
        var runner = new FakeShellCommandRunner(
            new ShellCommandResult(7, "", "failure"),
            new ShellCommandResult(0, "", ""));
        var executor = Executor(runner);
        var variables = Vars();

        await executor.ExecuteStepAsync("shell capture \"false\" code _ stderr 3 0 0", 2, variables, CancellationToken.None);

        runner.Calls.Should().ContainSingle();
        variables.Should().Contain("code", "7");
        variables.Should().Contain("stderr", "failure");
        variables.Should().NotContainKey("_");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenInputModeRuns_PassesResolvedStandardInput()
    {
        var runner = new FakeShellCommandRunner(new ShellCommandResult(0, "", ""));
        var executor = Executor(runner);
        var variables = Vars();
        variables["payload"] = "hello stdin";

        await executor.ExecuteStepAsync("shell input \"$payload\" \"cat\"", 2, variables, CancellationToken.None);

        runner.Calls.Should().ContainSingle();
        runner.Calls[0].Request.Command.Should().Be("cat");
        runner.Calls[0].Request.StandardInput.Should().Be("hello stdin");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCaptureInputModeRuns_PassesInputAndWritesVariables()
    {
        var runner = new FakeShellCommandRunner(new ShellCommandResult(0, "echoed", ""));
        var executor = Executor(runner);
        var variables = Vars();
        variables["payload"] = "hello";

        await executor.ExecuteStepAsync("shell capture-input \"$payload\" \"cat\" code out _", 2, variables, CancellationToken.None);

        runner.Calls.Should().ContainSingle();
        runner.Calls[0].Request.StandardInput.Should().Be("hello");
        variables.Should().Contain("code", "0");
        variables.Should().Contain("out", "echoed");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCommandKeepsFailing_ThrowsExitContext()
    {
        var runner = new FakeShellCommandRunner(
            new ShellCommandResult(7, "", "first failure"),
            new ShellCommandResult(9, "", "second failure"));
        var executor = Executor(runner);

        var act = async () => await executor.ExecuteStepAsync("shell \"false\" 1 0 0", 4, Vars(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Step 4*attempt 2/2*exited with code 9*second failure*");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenTimeoutRetriesAreExhausted_ThrowsAttemptContext()
    {
        var runner = new FakeShellCommandRunner(
            new ShellCommandTimeoutException("sleep", TimeSpan.FromMilliseconds(5)),
            new ShellCommandTimeoutException("sleep", TimeSpan.FromMilliseconds(5)));
        var executor = Executor(runner);

        var act = async () => await executor.ExecuteStepAsync("shell \"sleep\" 1 0 5", 8, Vars(), CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage("*Step 8*attempt 2/2*timed out after 5 ms*");
        runner.Calls.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenRunnerCancels_DoesNotRetry()
    {
        using var cts = new CancellationTokenSource();
        var runner = new FakeShellCommandRunner(new OperationCanceledException(cts.Token));
        var executor = Executor(runner);

        var act = async () => await executor.ExecuteStepAsync("shell \"sleep\" 3 0 0", 1, Vars(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        runner.Calls.Should().ContainSingle();
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenRunnerIsMissing_ThrowsMeaningfulError()
    {
        var executor = Executor(runner: null);

        var act = async () => await executor.ExecuteStepAsync("shell \"echo ok\"", 1, Vars(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*IShellCommandRunner*");
    }

    [Fact]
    public async Task PlayAsync_WhenShellOnlyScriptHasNoEvents_RunsWithoutSimulator()
    {
        var runner = new FakeShellCommandRunner(new ShellCommandResult(0, "", ""));
        using var player = CreatePlayer(
            runner,
            inputSimulatorFactory: () => throw new InvalidOperationException("simulator should not be acquired"));
        var macro = new MacroSequence
        {
            ScriptSteps = ["shell \"printf ok\""],
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        runner.Calls.Should().ContainSingle();
    }

    private static Dictionary<string, string> Vars() => new(StringComparer.OrdinalIgnoreCase);

    private static RunScriptShellExecutor Executor(IShellCommandRunner? runner)
    {
        var timingService = Substitute.For<IPlaybackTimingService>();
        var pauseToken = Substitute.For<IPlaybackPauseToken>();
        timingService.WaitAsync(Arg.Any<int>(), Arg.Any<IPlaybackPauseToken>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return new RunScriptShellExecutor(runner, timingService, pauseToken);
    }

    private static MacroPlayer CreatePlayer(
        IShellCommandRunner shellCommandRunner,
        Func<IInputSimulator>? inputSimulatorFactory = null)
    {
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        keyCodeMapper.IsModifierKeyCode(Arg.Any<int>()).Returns(returnThis: false);
        keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(-1);
        var positionProvider = Substitute.For<IMousePositionProvider>();
        positionProvider.ProviderName.Returns("fake-position");
        positionProvider.IsSupported.Returns(returnThis: true);

        return new MacroPlayer(
            positionProvider,
            new PlaybackValidator(keyCodeMapper, positionProvider),
            playbackWaitAsync: (_, _) => Task.CompletedTask,
            inputSimulatorFactory: inputSimulatorFactory ?? (() => Substitute.For<IInputSimulator>()),
            keyCodeMapper: keyCodeMapper,
            shellCommandRunner: shellCommandRunner);
    }

    private sealed class FakeShellCommandRunner : IShellCommandRunner
    {
        private readonly Queue<object> _outcomes;

        public FakeShellCommandRunner(params object[] outcomes)
        {
            _outcomes = new Queue<object>(outcomes);
        }

        public List<(ShellCommandRequest Request, TimeSpan? Timeout)> Calls { get; } = [];

        public Task<ShellCommandResult> RunAsync(ShellCommandRequest request, TimeSpan? timeout, CancellationToken cancellationToken = default)
        {
            Calls.Add((request, timeout));
            var outcome = _outcomes.Count > 0
                ? _outcomes.Dequeue()
                : new ShellCommandResult(0, "", "");
            return outcome switch
            {
                ShellCommandResult result => Task.FromResult(result),
                Exception exception => Task.FromException<ShellCommandResult>(exception),
                _ => throw new InvalidOperationException("Unsupported fake shell outcome."),
            };
        }
    }
}
