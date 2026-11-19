using CrossMacro.Commands.Runtime;

namespace CrossMacro.Cli.Tests;

public sealed class InputCommandHandlerTests
{
    private static readonly string[] TapSteps = ["tap A"];

    private readonly IRunScriptExecutionService _runService = Substitute.For<IRunScriptExecutionService>();
    private readonly IPreflightService _preflightService = Substitute.For<IPreflightService>();
    private readonly InputCommandHandler _handler;

    public InputCommandHandlerTests()
    {
        _ = _preflightService.CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult.Ok("Preflight checks passed."));
        _handler = new InputCommandHandler(_runService, _preflightService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreflightPasses_RunsTheSingleStepForReal()
    {
        _ = _runService.ExecuteAsync(Arg.Any<CliRunExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new OperationResult
            {
                Success = true,
                Outcome = ExecutionOutcomeCode.Success,
                Message = "Input executed.",
                Warnings = ["slow backend"],
                Data = "payload",
            });

        var result = await _handler.ExecuteAsync(new InputOptions("tap A"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Input executed.", result.Message);
        Assert.Equal("payload", result.Data);
        Assert.Equal(["slow backend"], result.Warnings);
        _ = await _preflightService.Received(1).CheckAsync(PreflightTarget.Run, CancellationToken.None);
        _ = await _runService.Received(1).ExecuteAsync(
            Arg.Is<CliRunExecutionRequest>(request => request.Steps.SequenceEqual(TapSteps) && !request.DryRun),
            CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDryRun_SkipsPreflightAndValidatesOnly()
    {
        _ = _preflightService.CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult.Fail(ExecutionOutcomeCode.EnvironmentError, "no simulator", ["simulator unsupported"]));
        _ = _runService.ExecuteAsync(Arg.Any<CliRunExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new OperationResult { Success = true, Outcome = ExecutionOutcomeCode.Success, Message = "ok" });

        var result = await _handler.ExecuteAsync(new InputOptions("tap A", DryRun: true), CancellationToken.None);

        Assert.True(result.Success);
        _ = await _preflightService.DidNotReceive().CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>());
        _ = await _runService.Received(1).ExecuteAsync(
            Arg.Is<CliRunExecutionRequest>(request => request.DryRun),
            CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreflightFails_ReturnsThePreflightFailureWithoutRunning()
    {
        _ = _preflightService.CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult.Fail(ExecutionOutcomeCode.EnvironmentError, "no simulator", ["simulator unsupported"]));

        var result = await _handler.ExecuteAsync(new InputOptions("tap A"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.EnvironmentError, result.Outcome);
        Assert.Equal("no simulator", result.Message);
        Assert.Equal(["simulator unsupported"], result.Errors);
        _ = await _runService.DidNotReceive().ExecuteAsync(Arg.Any<CliRunExecutionRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ExecutionOutcomeCode.InvalidArguments, ExecutionOutcomeCode.InvalidArguments)]
    [InlineData(ExecutionOutcomeCode.RuntimeError, ExecutionOutcomeCode.RuntimeError)]
    public async Task ExecuteAsync_WhenTheStepFails_MapsTheExitCodeAndKeepsTheErrors(ExecutionOutcomeCode outcome, ExecutionOutcomeCode expected)
    {
        _ = _runService.ExecuteAsync(Arg.Any<CliRunExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new OperationResult
            {
                Success = false,
                Outcome = outcome,
                Message = "Step failed.",
                Errors = ["Step 1: bad"],
            });

        var result = await _handler.ExecuteAsync(new InputOptions("bogus"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(["Step 1: bad"], result.Errors);
    }
}
