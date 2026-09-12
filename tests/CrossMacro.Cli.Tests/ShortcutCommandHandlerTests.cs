
namespace CrossMacro.Cli.Tests;

public sealed class ShortcutCommandHandlerTests
{
    [Fact]
    public void Constructor_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new ShortcutCommandHandler(shortcutCliService: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesToShortcutCliService()
    {
        var options = new ShortcutCliOptions(ShortcutCliAction.Add, Name: "Demo", MacroFilePath: "/tmp/demo.macro", Hotkey: "F7");
        var shortcutCliService = Substitute.For<IShortcutCliService>();
        _ = shortcutCliService.ExecuteAsync(options, Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Ok("Shortcut task added."));

        var handler = new ShortcutCommandHandler(shortcutCliService);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var result = await handler.ExecuteAsync(options, cancellationToken);

        Assert.True(result.Success);
        _ = await shortcutCliService.Received(1).ExecuteAsync(options, cancellationToken);
    }
}
