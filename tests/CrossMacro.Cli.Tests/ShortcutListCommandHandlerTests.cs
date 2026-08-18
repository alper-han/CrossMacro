
namespace CrossMacro.Cli.Tests;

public sealed class ShortcutListCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_LoadsAndReturnsTaskList()
    {
        var shortcutCliService = Substitute.For<IShortcutCliService>();
        _ = shortcutCliService.ListAsync(Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Ok("Loaded 1 shortcut task(s)."));

        var handler = new ShortcutListCommandHandler(shortcutCliService);
        var result = await handler.ExecuteAsync(new ShortcutListCliOptions(JsonOutput: true), CancellationToken.None);

        Assert.True(result.Success);
        _ = await shortcutCliService.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }
}
