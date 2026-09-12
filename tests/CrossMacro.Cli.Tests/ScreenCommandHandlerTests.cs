namespace CrossMacro.Cli.Tests;

public sealed class ScreenCommandHandlerTests
{
    [Fact]
    public void Constructor_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new ScreenCommandHandler(screenCliService: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesOptionsAndCancellation()
    {
        var screenCliService = Substitute.For<IScreenCliService>();
        var options = new ScreenCliOptions(ScreenCliAction.Pixel, X: 5, Y: -10, Relative: true);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        _ = screenCliService.ExecuteAsync(options, cancellationToken)
            .Returns(CliCommandExecutionResult.Ok("Pixel: (5, -10)"));

        var handler = new ScreenCommandHandler(screenCliService);
        var result = await handler.ExecuteAsync(options, cancellationToken);

        Assert.True(result.Success);
        _ = await screenCliService.Received(1).ExecuteAsync(options, cancellationToken);
    }
}
