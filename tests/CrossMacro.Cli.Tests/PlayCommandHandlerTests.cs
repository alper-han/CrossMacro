
namespace CrossMacro.Cli.Tests;

public sealed class PlayCommandHandlerTests
{
    private readonly IMacroExecutionService _executionService;
    private readonly IPreflightService _preflightService;
    private readonly PlayCommandHandler _handler;

    public PlayCommandHandlerTests()
    {
        _executionService = Substitute.For<IMacroExecutionService>();
        _preflightService = Substitute.For<IPreflightService>();
        _ = _preflightService.CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult.Ok("Preflight checks passed."));
        _handler = new PlayCommandHandler(_executionService, _preflightService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDryRun_ReturnsSuccess()
    {
        var options = new PlayOptions("/tmp/test.macro", DryRun: true);
        _ = _executionService.ExecuteAsync(Arg.Any<MacroExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new OperationResult
            {
                Success = true,
                Outcome = ExecutionOutcomeCode.Success,
                Message = "Macro is valid.",
            });

        var result = await _handler.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ExecutionOutcomeCode.Success, result.Outcome);
        _ = await _executionService.Received(1).ExecuteAsync(
            Arg.Is<MacroExecutionRequest>(x => x != null && x.DryRun && x.MacroFilePath == options.MacroFilePath),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDryRun_SkipsPreflight()
    {
        var options = new PlayOptions("/tmp/test.macro", DryRun: true);
        _ = _preflightService.CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult.Fail(
                ExecutionOutcomeCode.EnvironmentError,
                "Preflight check failed.",
                ["simulator unsupported"]));
        _ = _executionService.ExecuteAsync(Arg.Any<MacroExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new OperationResult
            {
                Success = true,
                Outcome = ExecutionOutcomeCode.Success,
                Message = "Macro is valid.",
            });

        var result = await _handler.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result.Success);
        _ = await _preflightService.DidNotReceive().CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>());
        _ = await _executionService.Received(1).ExecuteAsync(
            Arg.Is<MacroExecutionRequest>(x => x != null && x.DryRun && x.MacroFilePath == options.MacroFilePath),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceFails_PropagatesFailure()
    {
        var options = new PlayOptions("/tmp/test.macro");
        _ = _executionService.ExecuteAsync(Arg.Any<MacroExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new OperationResult
            {
                Success = false,
                Outcome = ExecutionOutcomeCode.RuntimeError,
                Message = "Playback failed.",
                Errors = ["simulator error"],
            });

        var result = await _handler.ExecuteAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.RuntimeError, result.Outcome);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreflightFails_ReturnsFailureWithoutCallingService()
    {
        _ = _preflightService.CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult.Fail(
                ExecutionOutcomeCode.EnvironmentError,
                "Preflight check failed.",
                ["display unsupported"]));

        var result = await _handler.ExecuteAsync(new PlayOptions("/tmp/test.macro"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.EnvironmentError, result.Outcome);
        _ = await _executionService.DidNotReceive().ExecuteAsync(Arg.Any<MacroExecutionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepeatCountProvidedWithoutLoop_UsesEffectiveLoop()
    {
        var options = new PlayOptions("/tmp/test.macro", Loop: false, RepeatCount: 50);
        _ = _executionService.ExecuteAsync(Arg.Any<MacroExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new OperationResult
            {
                Success = true,
                Outcome = ExecutionOutcomeCode.Success,
                Message = "Playback complete.",
            });

        var result = await _handler.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result.Success);
        _ = await _executionService.Received(1).ExecuteAsync(
            Arg.Is<MacroExecutionRequest>(x => x != null && x.Loop && x.RepeatCount == 50),
            Arg.Any<CancellationToken>());
    }
}
