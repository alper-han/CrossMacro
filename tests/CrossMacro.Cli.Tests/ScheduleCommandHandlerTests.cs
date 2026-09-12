
namespace CrossMacro.Cli.Tests;

public sealed class ScheduleCommandHandlerTests
{
    [Fact]
    public void Constructor_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new ScheduleCommandHandler(scheduleCliService: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesToScheduleCliService()
    {
        var options = new ScheduleCliOptions(ScheduleCliAction.Add, Name: "Daily", MacroFilePath: "/tmp/demo.macro");
        var scheduleCliService = Substitute.For<IScheduleCliService>();
        _ = scheduleCliService.ExecuteAsync(options, Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Ok("Schedule task added."));

        var handler = new ScheduleCommandHandler(scheduleCliService);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var result = await handler.ExecuteAsync(options, cancellationToken);

        Assert.True(result.Success);
        _ = await scheduleCliService.Received(1).ExecuteAsync(options, cancellationToken);
    }
}
