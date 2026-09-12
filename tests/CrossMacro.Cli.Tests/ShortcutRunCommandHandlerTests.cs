
namespace CrossMacro.Cli.Tests;

public sealed class ShortcutRunCommandHandlerTests
{
    [Fact]
    public void Constructor_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new ShortcutRunCommandHandler(shortcutCliService: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesToShortcutCliService()
    {
        var shortcutCliService = Substitute.For<IShortcutCliService>();
        _ = shortcutCliService.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Ok("Shortcut task executed."));

        var handler = new ShortcutRunCommandHandler(shortcutCliService);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var result = await handler.ExecuteAsync(new ShortcutRunCliOptions("22222222-2222-2222-2222-222222222222"), cancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Shortcut task executed.", result.Message);
        _ = await shortcutCliService.Received(1).RunAsync("22222222-2222-2222-2222-222222222222", cancellationToken);
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
        Assert.Equal("Shortcut task not found.", result.Message);
        Assert.Equal(["No shortcut task found with id: 22222222-2222-2222-2222-222222222222"], result.Errors);
        _ = await shortcutCliService.Received(1).RunAsync("22222222-2222-2222-2222-222222222222", CancellationToken.None);
    }
}
