
namespace CrossMacro.Cli.Tests;

public sealed class ShortcutRunCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_DelegatesToShortcutCliService()
    {
        var shortcutCliService = Substitute.For<IShortcutCliService>();
        _ = shortcutCliService.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Ok("Shortcut task executed."));

        var handler = new ShortcutRunCommandHandler(shortcutCliService);
        var result = await handler.ExecuteAsync(new ShortcutRunCliOptions("22222222-2222-2222-2222-222222222222"), CancellationToken.None);

        Assert.True(result.Success);
        _ = await shortcutCliService.Received(1).RunAsync("22222222-2222-2222-2222-222222222222", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceReturnsFailure_PropagatesFailure()
    {
        var shortcutCliService = Substitute.For<IShortcutCliService>();
        _ = shortcutCliService.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "Shortcut task not found.",
                errors: ["No shortcut task found with id: 22222222-2222-2222-2222-222222222222"]));

        var handler = new ShortcutRunCommandHandler(shortcutCliService);
        var result = await handler.ExecuteAsync(
            new ShortcutRunCliOptions("22222222-2222-2222-2222-222222222222"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        _ = await shortcutCliService.Received(1).RunAsync("22222222-2222-2222-2222-222222222222", Arg.Any<CancellationToken>());
    }
}
