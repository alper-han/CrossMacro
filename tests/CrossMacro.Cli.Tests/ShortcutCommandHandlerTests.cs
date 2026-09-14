
namespace CrossMacro.Cli.Tests;

public sealed class ShortcutCommandHandlerTests
{
    [Fact]
    public void Constructor_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new ShortcutCommandHandler(commands: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesToApplicationCommands()
    {
        var options = new ShortcutCliOptions(ShortcutCliAction.Add, Name: "Demo", MacroFilePath: "/tmp/demo.macro", Hotkey: "F7");
        var commands = Substitute.For<IShortcutCommands>();
        _ = commands.ExecuteAsync(TaskCommandOptionsMapper.ToApplication(options), Arg.Any<CancellationToken>())
            .Returns(TaskCommandResult.Ok<ShortcutTask>("Shortcut task added."));

        var handler = new ShortcutCommandHandler(commands);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var result = await handler.ExecuteAsync(options, cancellationToken);

        Assert.True(result.Success);
        _ = await commands.Received(1).ExecuteAsync(TaskCommandOptionsMapper.ToApplication(options), cancellationToken);
    }
}
