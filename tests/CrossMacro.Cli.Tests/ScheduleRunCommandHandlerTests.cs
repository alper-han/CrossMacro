
namespace CrossMacro.Cli.Tests;

public sealed class ScheduleRunCommandHandlerTests
{
    [Fact]
    public void Constructor_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new ScheduleRunCommandHandler(commands: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesToApplicationCommands()
    {
        var commands = Substitute.For<IScheduleCommands>();
        _ = commands.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TaskCommandResult.Ok<ScheduledTask>("Schedule task executed."));

        var handler = new ScheduleRunCommandHandler(commands);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var result = await handler.ExecuteAsync(new ScheduleRunCliOptions("11111111-1111-1111-1111-111111111111"), cancellationToken);

        Assert.True(result.Success);
        _ = await commands.Received(1).RunAsync("11111111-1111-1111-1111-111111111111", cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceReturnsFailure_PropagatesFailure()
    {
        var commands = Substitute.For<IScheduleCommands>();
        _ = commands.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TaskCommandResult.Fail<ScheduledTask>("Schedule task not found.",
                errors: ["No schedule task found with id: 11111111-1111-1111-1111-111111111111"]));

        var handler = new ScheduleRunCommandHandler(commands);
        var result = await handler.ExecuteAsync(
            new ScheduleRunCliOptions("11111111-1111-1111-1111-111111111111"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        _ = await commands.Received(1).RunAsync("11111111-1111-1111-1111-111111111111", CancellationToken.None);
    }
}
