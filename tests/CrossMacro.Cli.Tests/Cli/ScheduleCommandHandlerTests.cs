
namespace CrossMacro.Cli.Tests;

public class ScheduleCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_DelegatesToScheduleCliService()
    {
        var options = new ScheduleCliOptions(ScheduleCliAction.Add, Name: "Daily", MacroFilePath: "/tmp/demo.macro");
        var scheduleCliService = Substitute.For<IScheduleCliService>();
        scheduleCliService.ExecuteAsync(options, Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Ok("Schedule task added."));

        var handler = new ScheduleCommandHandler(scheduleCliService);
        var result = await handler.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result.Success);
        await scheduleCliService.Received(1).ExecuteAsync(options, Arg.Any<CancellationToken>());
    }
}
