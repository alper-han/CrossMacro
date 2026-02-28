using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli;
using CrossMacro.Cli.Commands;
using CrossMacro.Cli.Services;
using NSubstitute;
using Xunit;

namespace CrossMacro.Cli.Tests;

public class ScheduleListCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_LoadsAndReturnsTaskList()
    {
        var scheduleCliService = Substitute.For<IScheduleCliService>();
        scheduleCliService.ListAsync(Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Ok("Loaded 1 schedule task(s)."));

        var handler = new ScheduleListCommandHandler(scheduleCliService);
        var result = await handler.ExecuteAsync(new ScheduleListCliOptions(JsonOutput: true), CancellationToken.None);

        Assert.True(result.Success);
        await scheduleCliService.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }
}
