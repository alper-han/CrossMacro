
namespace CrossMacro.Cli.Tests;

public sealed class ShortcutListCommandHandlerTests
{
    [Fact]
    public void Constructor_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new ShortcutListCommandHandler(commands: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_LoadsAndReturnsTaskList()
    {
        var commands = Substitute.For<IShortcutCommands>();
        _ = commands.ListAsync(Arg.Any<CancellationToken>())
            .Returns(TaskCommandResult.Ok<ShortcutTask>("Loaded 1 shortcut task(s)."));

        var handler = new ShortcutListCommandHandler(commands);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var result = await handler.ExecuteAsync(new ShortcutListCliOptions(JsonOutput: true), cancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Loaded 1 shortcut task(s).", result.Message);
        _ = await commands.Received(1).ListAsync(cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceFails_PropagatesFailure()
    {
        var commands = Substitute.For<IShortcutCommands>();
        _ = commands.ListAsync(Arg.Any<CancellationToken>())
            .Returns(TaskCommandResult.Fail<ShortcutTask>("Shortcuts unavailable.",
                errors: ["Shortcut store could not be loaded."]));

        var handler = new ShortcutListCommandHandler(commands);
        var result = await handler.ExecuteAsync(new ShortcutListCliOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Equal("Shortcuts unavailable.", result.Message);
        Assert.Equal(["Shortcut store could not be loaded."], result.Errors);
    }
}
