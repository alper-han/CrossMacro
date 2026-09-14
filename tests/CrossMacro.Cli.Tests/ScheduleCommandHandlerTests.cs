
namespace CrossMacro.Cli.Tests;

public sealed class ScheduleCommandHandlerTests
{
    [Fact]
    public void Constructor_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new ScheduleCommandHandler(commands: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesToApplicationCommands()
    {
        var options = new ScheduleCliOptions(ScheduleCliAction.Add, Name: "Daily", MacroFilePath: "/tmp/demo.macro");
        var commands = Substitute.For<IScheduleCommands>();
        _ = commands.ExecuteAsync(TaskCommandOptionsMapper.ToApplication(options), Arg.Any<CancellationToken>())
            .Returns(TaskCommandResult.Ok<ScheduledTask>("Schedule task added."));

        var handler = new ScheduleCommandHandler(commands);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var result = await handler.ExecuteAsync(options, cancellationToken);

        Assert.True(result.Success);
        _ = await commands.Received(1).ExecuteAsync(TaskCommandOptionsMapper.ToApplication(options), cancellationToken);
    }
}
