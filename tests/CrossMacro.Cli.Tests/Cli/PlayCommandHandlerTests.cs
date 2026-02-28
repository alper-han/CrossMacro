using CrossMacro.Cli;
using CrossMacro.Cli.Commands;
using CrossMacro.Cli.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class PlayCommandHandlerTests
{
    private readonly IMacroExecutionService _executionService;
    private readonly ICliPreflightService _preflightService;
    private readonly PlayCommandHandler _handler;

    public PlayCommandHandlerTests()
    {
        _executionService = Substitute.For<IMacroExecutionService>();
        _preflightService = Substitute.For<ICliPreflightService>();
        _preflightService.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Ok());
        _handler = new PlayCommandHandler(_executionService, _preflightService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDryRun_ReturnsSuccess()
    {
        var options = new PlayCliOptions("/tmp/test.macro", DryRun: true);
        _executionService.ExecuteAsync(Arg.Any<MacroExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Macro is valid."
            });

        var result = await _handler.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        await _executionService.Received(1).ExecuteAsync(
            Arg.Is<MacroExecutionRequest>(x => x.DryRun && x.MacroFilePath == options.MacroFilePath),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDryRun_SkipsPreflight()
    {
        var options = new PlayCliOptions("/tmp/test.macro", DryRun: true);
        _preflightService.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Fail(
                CliExitCode.EnvironmentError,
                "Preflight check failed.",
                ["simulator unsupported"]));
        _executionService.ExecuteAsync(Arg.Any<MacroExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Macro is valid."
            });

        var result = await _handler.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result.Success);
        await _preflightService.DidNotReceive().CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>());
        await _executionService.Received(1).ExecuteAsync(
            Arg.Is<MacroExecutionRequest>(x => x.DryRun && x.MacroFilePath == options.MacroFilePath),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceFails_PropagatesFailure()
    {
        var options = new PlayCliOptions("/tmp/test.macro");
        _executionService.ExecuteAsync(Arg.Any<MacroExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.RuntimeError,
                Message = "Playback failed.",
                Errors = ["simulator error"]
            });

        var result = await _handler.ExecuteAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.RuntimeError, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreflightFails_ReturnsFailureWithoutCallingService()
    {
        _preflightService.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Fail(
                CliExitCode.EnvironmentError,
                "Preflight check failed.",
                ["display unsupported"]));

        var result = await _handler.ExecuteAsync(new PlayCliOptions("/tmp/test.macro"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
        await _executionService.DidNotReceive().ExecuteAsync(Arg.Any<MacroExecutionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepeatCountProvidedWithoutLoop_UsesEffectiveLoop()
    {
        var options = new PlayCliOptions("/tmp/test.macro", Loop: false, RepeatCount: 50);
        _executionService.ExecuteAsync(Arg.Any<MacroExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Playback complete."
            });

        var result = await _handler.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result.Success);
        await _executionService.Received(1).ExecuteAsync(
            Arg.Is<MacroExecutionRequest>(x => x.Loop && x.RepeatCount == 50),
            Arg.Any<CancellationToken>());
    }
}
