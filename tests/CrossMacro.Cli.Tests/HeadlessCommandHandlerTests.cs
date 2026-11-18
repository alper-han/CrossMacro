
namespace CrossMacro.Cli.Tests;

public sealed class HeadlessCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenRuntimeSucceeds_ReturnsSuccess()
    {
        var runtime = Substitute.For<IHeadlessRuntimeService>();
        var preflight = Substitute.For<IPreflightService>();
        _ = preflight.CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult.Ok("Preflight checks passed."));
        _ = runtime.RunAsync(Arg.Any<CancellationToken>())
            .Returns(new OperationResult
            {
                Success = true,
                Outcome = ExecutionOutcomeCode.Success,
                Message = "Headless mode stopped.",
            });

        var handler = new HeadlessCommandHandler(runtime, preflight);
        var result = await handler.ExecuteAsync(new HeadlessOptions(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ExecutionOutcomeCode.Success, result.Outcome);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRuntimeFails_ReturnsFailure()
    {
        var runtime = Substitute.For<IHeadlessRuntimeService>();
        var preflight = Substitute.For<IPreflightService>();
        _ = preflight.CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult.Ok("Preflight checks passed."));
        _ = runtime.RunAsync(Arg.Any<CancellationToken>())
            .Returns(new OperationResult
            {
                Success = false,
                Outcome = ExecutionOutcomeCode.EnvironmentError,
                Message = "Failed to start headless mode.",
                Errors = ["unsupported display"],
            });

        var handler = new HeadlessCommandHandler(runtime, preflight);
        var result = await handler.ExecuteAsync(new HeadlessOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.EnvironmentError, result.Outcome);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreflightFails_ReturnsFailure()
    {
        var runtime = Substitute.For<IHeadlessRuntimeService>();
        var preflight = Substitute.For<IPreflightService>();
        _ = preflight.CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult.Fail(
                ExecutionOutcomeCode.EnvironmentError,
                "Preflight check failed.",
                ["display session unsupported"]));

        var handler = new HeadlessCommandHandler(runtime, preflight);
        var result = await handler.ExecuteAsync(new HeadlessOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.EnvironmentError, result.Outcome);
        _ = await runtime.DidNotReceive().RunAsync(Arg.Any<CancellationToken>());
    }
}
