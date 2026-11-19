using CrossMacro.Application.Execution;
using CrossMacro.Application.Runtime;

namespace CrossMacro.Application.Tests.Execution;

public sealed class RunScriptExecutionWorkflowTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "crossmacro-run-steps-" + Guid.NewGuid().ToString("N"));
    private readonly IRunExecutionService _runtime = Substitute.For<IRunExecutionService>();
    private RunExecutionRequest? _forwarded;

    public RunScriptExecutionWorkflowTests()
    {
        _ = Directory.CreateDirectory(_directory);
        _ = _runtime.ExecuteAsync(Arg.Any<RunExecutionRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _forwarded = call.Arg<RunExecutionRequest>();
            return new RunExecutionResult { Status = RunExecutionStatus.Succeeded, StepCount = _forwarded.Steps.Count };
        });
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task StepFile_SkipsBlankAndCommentLinesAndKeepsLineNumbers()
    {
        var path = WriteFile("steps.txt", "# comment\r\n\r\n  move 1 2  \r\n#another\nclick left\n");

        var result = await new RunScriptExecutionWorkflow(_runtime).ExecuteAsync(
            new RunScriptExecutionRequest { StepFilePath = path, Steps = ["inline"] },
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(_forwarded);
        Assert.Collection(
            _forwarded.Steps,
            step => AssertStep(step, "move 1 2", fileLine: 3, sourceIndex: 1),
            step => AssertStep(step, "click left", fileLine: 5, sourceIndex: 2),
            step => AssertStep(step, "inline", fileLine: null, sourceIndex: 3));
    }

    [Fact]
    public async Task StepFile_WhenMissing_FailsWithFileError()
    {
        var result = await new RunScriptExecutionWorkflow(_runtime).ExecuteAsync(
            new RunScriptExecutionRequest { StepFilePath = Path.Combine(_directory, "missing.txt") },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.FileError, result.Outcome);
        _ = await _runtime.DidNotReceive().ExecuteAsync(Arg.Any<RunExecutionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StepFile_WhenEmpty_FailsWithFileError()
    {
        var path = WriteFile("empty.txt", string.Empty);

        var result = await new RunScriptExecutionWorkflow(_runtime).ExecuteAsync(
            new RunScriptExecutionRequest { StepFilePath = path },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.FileError, result.Outcome);
    }

    [Fact]
    public async Task StepFile_LinesLongerThanTheReadBuffer_AreReadWhole()
    {
        var first = "type " + new string('a', 4091);
        var second = "type " + new string('b', 9000);
        var last = "type " + new string('c', 5000);
        var path = WriteFile("long-lines.txt", first + "\r\n" + second + "\n\n" + last);

        var result = await new RunScriptExecutionWorkflow(_runtime).ExecuteAsync(
            new RunScriptExecutionRequest { StepFilePath = path },
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(_forwarded);
        Assert.Collection(
            _forwarded.Steps,
            step => AssertStep(step, first, fileLine: 1, sourceIndex: 1),
            step => AssertStep(step, second, fileLine: 2, sourceIndex: 2),
            step => AssertStep(step, last, fileLine: 4, sourceIndex: 3));
    }

    [Fact]
    public async Task StepFile_WithTooManyLines_FailsWithFileError()
    {
        var path = WriteFile("many.txt", string.Concat(Enumerable.Repeat("#\n", 10_000_001)));

        var result = await new RunScriptExecutionWorkflow(_runtime).ExecuteAsync(
            new RunScriptExecutionRequest { StepFilePath = path },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.FileError, result.Outcome);
        Assert.Contains("lines", Assert.Single(result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StepFile_CancelledWhileReading_PropagatesCancellation()
    {
        var path = WriteFile("steps.txt", "move 1 2\n");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RunScriptExecutionWorkflow(_runtime).ExecuteAsync(
            new RunScriptExecutionRequest { StepFilePath = path },
            cancellation.Token));
    }

    [Fact]
    public async Task NoSteps_IsRejectedBeforeTheRuntimeIsCalled()
    {
        var result = await new RunScriptExecutionWorkflow(_runtime).ExecuteAsync(
            new RunScriptExecutionRequest(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.Outcome);
        _ = await _runtime.DidNotReceive().ExecuteAsync(Arg.Any<RunExecutionRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(RunExecutionStatus.InvalidArguments, ExecutionOutcomeCode.InvalidArguments)]
    [InlineData(RunExecutionStatus.ValidationFailed, ExecutionOutcomeCode.ValidationError)]
    [InlineData(RunExecutionStatus.Cancelled, ExecutionOutcomeCode.Cancelled)]
    [InlineData(RunExecutionStatus.Failed, ExecutionOutcomeCode.RuntimeError)]
    [InlineData(RunExecutionStatus.InputInjectionPermissionRequired, ExecutionOutcomeCode.EnvironmentError)]
    [InlineData(RunExecutionStatus.AbsolutePlaybackUnsupported, ExecutionOutcomeCode.RuntimeError)]
    public async Task RuntimeStatus_IsMappedToTheMatchingExitCode(RunExecutionStatus status, ExecutionOutcomeCode expected)
    {
        _ = _runtime.ExecuteAsync(Arg.Any<RunExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RunExecutionResult { Status = status, ErrorMessage = "boom", Errors = ["boom"] });

        var result = await new RunScriptExecutionWorkflow(_runtime).ExecuteAsync(
            new RunScriptExecutionRequest { Steps = ["move 1 2"] },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(expected, result.Outcome);
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static void AssertStep(RunScriptInputStep step, string text, int? fileLine, int sourceIndex)
    {
        Assert.Equal(text, step.Step);
        Assert.Equal(fileLine, step.SourceLineNumber);
        Assert.Equal(sourceIndex, step.SourceIndex);
    }
}
