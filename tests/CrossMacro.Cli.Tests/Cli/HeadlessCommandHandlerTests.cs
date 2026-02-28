using CrossMacro.Cli;
using CrossMacro.Cli.Commands;
using CrossMacro.Cli.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class HeadlessCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenRuntimeSucceeds_ReturnsSuccess()
    {
        var runtime = Substitute.For<IHeadlessRuntimeService>();
        var preflight = Substitute.For<ICliPreflightService>();
        preflight.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Ok());
        runtime.RunAsync(Arg.Any<CancellationToken>())
            .Returns(new HeadlessRuntimeResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Headless mode stopped."
            });

        var handler = new HeadlessCommandHandler(runtime, preflight);
        var result = await handler.ExecuteAsync(new HeadlessCliOptions(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRuntimeFails_ReturnsFailure()
    {
        var runtime = Substitute.For<IHeadlessRuntimeService>();
        var preflight = Substitute.For<ICliPreflightService>();
        preflight.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Ok());
        runtime.RunAsync(Arg.Any<CancellationToken>())
            .Returns(new HeadlessRuntimeResult
            {
                Success = false,
                ExitCode = CliExitCode.EnvironmentError,
                Message = "Failed to start headless mode.",
                Errors = ["unsupported display"]
            });

        var handler = new HeadlessCommandHandler(runtime, preflight);
        var result = await handler.ExecuteAsync(new HeadlessCliOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreflightFails_ReturnsFailure()
    {
        var runtime = Substitute.For<IHeadlessRuntimeService>();
        var preflight = Substitute.For<ICliPreflightService>();
        preflight.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Fail(
                CliExitCode.EnvironmentError,
                "Preflight check failed.",
                ["display session unsupported"]));

        var handler = new HeadlessCommandHandler(runtime, preflight);
        var result = await handler.ExecuteAsync(new HeadlessCliOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
        await runtime.DidNotReceive().RunAsync(Arg.Any<CancellationToken>());
    }
}
