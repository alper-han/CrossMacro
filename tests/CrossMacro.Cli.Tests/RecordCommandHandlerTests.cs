
namespace CrossMacro.Cli.Tests;

public sealed class RecordCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenServiceSucceeds_ReturnsSuccess()
    {
        var service = Substitute.For<IRecordExecutionService>();
        var preflight = Substitute.For<ICliPreflightService>();
        _ = preflight.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Ok());
        _ = service.ExecuteAsync(Arg.Any<RecordExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RecordExecutionResult
            {
                Success = true,
                ExitCode = ExecutionOutcomeCode.Success,
                Message = "Recording completed.",
            });

        var handler = new RecordCommandHandler(service, preflight);
        var options = new RecordCliOptions(
            "/tmp/out.macro",
            RecordMouse: false,
            RecordKeyboard: true,
            CoordinateMode: RecordCoordinateMode.Absolute,
            SkipInitialZero: true,
            DurationSeconds: 12);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var result = await handler.ExecuteAsync(options, cancellationToken);

        Assert.True(result.Success);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        _ = await preflight.Received(1).CheckAsync(CliPreflightTarget.Record, cancellationToken);
        _ = await service.Received(1).ExecuteAsync(
            Arg.Is<RecordExecutionRequest>(request => request.OutputFilePath == options.OutputFilePath
                && !request.RecordMouse
                && request.RecordKeyboard
                && request.CoordinateMode == options.CoordinateMode
                && request.SkipInitialZero
                && request.DurationSeconds == options.DurationSeconds),
            cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceFails_ReturnsFailure()
    {
        var service = Substitute.For<IRecordExecutionService>();
        var preflight = Substitute.For<ICliPreflightService>();
        _ = preflight.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Ok());
        _ = service.ExecuteAsync(Arg.Any<RecordExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RecordExecutionResult
            {
                Success = false,
                ExitCode = ExecutionOutcomeCode.EnvironmentError,
                Message = "Failed to start recording.",
                Errors = ["capture unavailable"],
            });

        var handler = new RecordCommandHandler(service, preflight);
        var result = await handler.ExecuteAsync(new RecordCliOptions("/tmp/out.macro"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreflightFails_ReturnsFailure()
    {
        var service = Substitute.For<IRecordExecutionService>();
        var preflight = Substitute.For<ICliPreflightService>();
        _ = preflight.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Fail(
                CliExitCode.EnvironmentError,
                "Preflight check failed.",
                ["capture backend unavailable"]));

        var handler = new RecordCommandHandler(service, preflight);
        var result = await handler.ExecuteAsync(new RecordCliOptions("/tmp/out.macro"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
        _ = await service.DidNotReceive().ExecuteAsync(Arg.Any<RecordExecutionRequest>(), Arg.Any<CancellationToken>());
    }
}
