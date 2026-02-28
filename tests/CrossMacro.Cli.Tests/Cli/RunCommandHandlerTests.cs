using CrossMacro.Cli;
using CrossMacro.Cli.Commands;
using CrossMacro.Cli.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class RunCommandHandlerTests
{
    private readonly IRunScriptExecutionService _runService;
    private readonly ICliPreflightService _preflightService;
    private readonly RunCommandHandler _handler;

    public RunCommandHandlerTests()
    {
        _runService = Substitute.For<IRunScriptExecutionService>();
        _preflightService = Substitute.For<ICliPreflightService>();
        _preflightService.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Ok());
        _handler = new RunCommandHandler(_runService, _preflightService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceSucceeds_ReturnsSuccess()
    {
        _runService.ExecuteAsync(Arg.Any<RunExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Run script execution complete."
            });

        var result = await _handler.ExecuteAsync(
            new RunCliOptions(["move abs 10 10", "click left"], StepFilePath: "/tmp/steps.txt", DryRun: true),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        await _runService.Received(1).ExecuteAsync(
            Arg.Is<RunExecutionRequest>(x => x.Steps.Count == 2 && x.StepFilePath == "/tmp/steps.txt" && x.DryRun),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDryRun_SkipsPreflight()
    {
        _preflightService.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Fail(
                CliExitCode.EnvironmentError,
                "Preflight check failed.",
                ["simulator unsupported"]));
        _runService.ExecuteAsync(Arg.Any<RunExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Run script parsed successfully (dry-run)."
            });

        var result = await _handler.ExecuteAsync(
            new RunCliOptions(["click left"], DryRun: true),
            CancellationToken.None);

        Assert.True(result.Success);
        await _preflightService.DidNotReceive().CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>());
        await _runService.Received(1).ExecuteAsync(
            Arg.Is<RunExecutionRequest>(x => x.DryRun && x.Steps.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceFails_PropagatesFailure()
    {
        _runService.ExecuteAsync(Arg.Any<RunExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Run script parsing failed.",
                Errors = ["Step 1: bad syntax"]
            });

        var result = await _handler.ExecuteAsync(
            new RunCliOptions(["bad-step"]),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreflightFails_ReturnsFailureWithoutCallingService()
    {
        _preflightService.CheckAsync(Arg.Any<CliPreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(CliPreflightResult.Fail(
                CliExitCode.EnvironmentError,
                "Preflight check failed.",
                ["simulator unsupported"]));

        var result = await _handler.ExecuteAsync(new RunCliOptions(["click left"]), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
        await _runService.DidNotReceive().ExecuteAsync(Arg.Any<RunExecutionRequest>(), Arg.Any<CancellationToken>());
    }
}
