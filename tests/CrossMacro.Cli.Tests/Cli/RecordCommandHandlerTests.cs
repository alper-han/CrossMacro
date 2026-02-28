using CrossMacro.Cli;
using CrossMacro.Cli.Commands;
using CrossMacro.Cli.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class RecordCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenServiceSucceeds_ReturnsSuccess()
    {
        var service = Substitute.For<IRecordExecutionService>();
        var preflight = Substitute.For<ICliPreflightService>();
        preflight.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Ok());
        service.ExecuteAsync(Arg.Any<RecordExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RecordExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Recording completed."
            });

        var handler = new RecordCommandHandler(service, preflight);
        var result = await handler.ExecuteAsync(new RecordCliOptions("/tmp/out.macro"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceFails_ReturnsFailure()
    {
        var service = Substitute.For<IRecordExecutionService>();
        var preflight = Substitute.For<ICliPreflightService>();
        preflight.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Ok());
        service.ExecuteAsync(Arg.Any<RecordExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RecordExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.EnvironmentError,
                Message = "Failed to start recording.",
                Errors = ["capture unavailable"]
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
        preflight.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Fail(
                CliExitCode.EnvironmentError,
                "Preflight check failed.",
                ["capture backend unavailable"]));

        var handler = new RecordCommandHandler(service, preflight);
        var result = await handler.ExecuteAsync(new RecordCliOptions("/tmp/out.macro"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
        await service.DidNotReceive().ExecuteAsync(Arg.Any<RecordExecutionRequest>(), Arg.Any<CancellationToken>());
    }
}
