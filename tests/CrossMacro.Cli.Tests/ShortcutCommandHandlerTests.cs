
namespace CrossMacro.Cli.Tests;

public sealed class ShortcutCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_DelegatesToShortcutCliService()
    {
        var options = new ShortcutCliOptions(ShortcutCliAction.Add, Name: "Demo", MacroFilePath: "/tmp/demo.macro", Hotkey: "F7");
        var shortcutCliService = Substitute.For<IShortcutCliService>();
        _ = shortcutCliService.ExecuteAsync(options, Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Ok("Shortcut task added."));

        var handler = new ShortcutCommandHandler(shortcutCliService);
        var result = await handler.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result.Success);
        _ = await shortcutCliService.Received(1).ExecuteAsync(options, Arg.Any<CancellationToken>());
    }
}
