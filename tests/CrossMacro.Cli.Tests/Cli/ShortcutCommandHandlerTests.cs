
namespace CrossMacro.Cli.Tests;

public class ShortcutCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_DelegatesToShortcutCliService()
    {
        var options = new ShortcutCliOptions(ShortcutCliAction.Add, Name: "Demo", MacroFilePath: "/tmp/demo.macro", Hotkey: "F7");
        var shortcutCliService = Substitute.For<IShortcutCliService>();
        shortcutCliService.ExecuteAsync(options, Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Ok("Shortcut task added."));

        var handler = new ShortcutCommandHandler(shortcutCliService);
        var result = await handler.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result.Success);
        await shortcutCliService.Received(1).ExecuteAsync(options, Arg.Any<CancellationToken>());
    }
}
