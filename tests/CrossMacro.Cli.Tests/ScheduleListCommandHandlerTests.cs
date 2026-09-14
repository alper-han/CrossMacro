
namespace CrossMacro.Cli.Tests;

public sealed class ScheduleListCommandHandlerTests
{
    [Fact]
    public void Constructor_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new ScheduleListCommandHandler(commands: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_LoadsAndReturnsTaskList()
    {
        var commands = Substitute.For<IScheduleCommands>();
        _ = commands.ListAsync(Arg.Any<CancellationToken>())
            .Returns(TaskCommandResult.Ok<ScheduledTask>("Loaded 1 schedule task(s)."));

        var handler = new ScheduleListCommandHandler(commands);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var result = await handler.ExecuteAsync(new ScheduleListCliOptions(JsonOutput: true), cancellationToken);

        Assert.True(result.Success);
        _ = await commands.Received(1).ListAsync(cancellationToken);
    }
}
