using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli;
using CrossMacro.Cli.Commands;
using CrossMacro.Cli.Services;
using NSubstitute;
using Xunit;

namespace CrossMacro.Cli.Tests;

public class ScheduleRunCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_DelegatesToScheduleCliService()
    {
        var scheduleCliService = Substitute.For<IScheduleCliService>();
        scheduleCliService.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Ok("Schedule task executed."));

        var handler = new ScheduleRunCommandHandler(scheduleCliService);
        var result = await handler.ExecuteAsync(new ScheduleRunCliOptions("11111111-1111-1111-1111-111111111111"), CancellationToken.None);

        Assert.True(result.Success);
        await scheduleCliService.Received(1).RunAsync("11111111-1111-1111-1111-111111111111", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceReturnsFailure_PropagatesFailure()
    {
        var scheduleCliService = Substitute.For<IScheduleCliService>();
        scheduleCliService.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "Schedule task not found.",
                errors: ["No schedule task found with id: 11111111-1111-1111-1111-111111111111"]));

        var handler = new ScheduleRunCommandHandler(scheduleCliService);
        var result = await handler.ExecuteAsync(
            new ScheduleRunCliOptions("11111111-1111-1111-1111-111111111111"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        await scheduleCliService.Received(1).RunAsync("11111111-1111-1111-1111-111111111111", Arg.Any<CancellationToken>());
    }
}
