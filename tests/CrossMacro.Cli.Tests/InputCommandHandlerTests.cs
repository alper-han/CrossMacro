
namespace CrossMacro.Cli.Tests;

public sealed class InputCommandHandlerTests
{
    private static readonly string[] TapSteps = ["tap A"];

    private readonly IRunScriptExecutionService _runService = Substitute.For<IRunScriptExecutionService>();
    private readonly ICliPreflightService _preflightService = Substitute.For<ICliPreflightService>();
    private readonly InputCommandHandler _handler;

    public InputCommandHandlerTests()
    {
        _ = _preflightService.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Ok());
        _handler = new InputCommandHandler(_runService, _preflightService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreflightPasses_RunsTheSingleStepForReal()
    {
        _ = _runService.ExecuteAsync(Arg.Any<CliRunExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MacroExecutionResult
            {
                Success = true,
                ExitCode = ExecutionOutcomeCode.Success,
                Message = "Input executed.",
                Warnings = ["slow backend"],
                Data = "payload",
            });

        var result = await _handler.ExecuteAsync(new InputCliOptions("tap A"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Input executed.", result.Message);
        Assert.Equal("payload", result.Data);
        Assert.Equal(["slow backend"], result.Warnings);
        _ = await _preflightService.Received(1).CheckAsync(CliPreflightTarget.Run, CancellationToken.None);
        _ = await _runService.Received(1).ExecuteAsync(
            Arg.Is<CliRunExecutionRequest>(request => request.Steps.SequenceEqual(TapSteps) && !request.DryRun),
            CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDryRun_SkipsPreflightAndValidatesOnly()
    {
        _ = _preflightService.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Fail(CliExitCode.EnvironmentError, "no simulator", ["simulator unsupported"]));
        _ = _runService.ExecuteAsync(Arg.Any<CliRunExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MacroExecutionResult { Success = true, ExitCode = ExecutionOutcomeCode.Success, Message = "ok" });

        var result = await _handler.ExecuteAsync(new InputCliOptions("tap A", DryRun: true), CancellationToken.None);

        Assert.True(result.Success);
        _ = await _preflightService.DidNotReceive().CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>());
        _ = await _runService.Received(1).ExecuteAsync(
            Arg.Is<CliRunExecutionRequest>(request => request.DryRun),
            CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreflightFails_ReturnsThePreflightFailureWithoutRunning()
    {
        _ = _preflightService.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Fail(CliExitCode.EnvironmentError, "no simulator", ["simulator unsupported"]));

        var result = await _handler.ExecuteAsync(new InputCliOptions("tap A"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Equal("no simulator", result.Message);
        Assert.Equal(["simulator unsupported"], result.Errors);
        _ = await _runService.DidNotReceive().ExecuteAsync(Arg.Any<CliRunExecutionRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ExecutionOutcomeCode.InvalidArguments, CliExitCode.InvalidArguments)]
    [InlineData(ExecutionOutcomeCode.RuntimeError, CliExitCode.RuntimeError)]
    public async Task ExecuteAsync_WhenTheStepFails_MapsTheExitCodeAndKeepsTheErrors(ExecutionOutcomeCode outcome, CliExitCode expected)
    {
        _ = _runService.ExecuteAsync(Arg.Any<CliRunExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MacroExecutionResult
            {
                Success = false,
                ExitCode = outcome,
                Message = "Step failed.",
                Errors = ["Step 1: bad"],
            });

        var result = await _handler.ExecuteAsync(new InputCliOptions("bogus"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)expected, result.ExitCode);
        Assert.Equal(["Step 1: bad"], result.Errors);
    }
}
