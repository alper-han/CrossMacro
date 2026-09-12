namespace CrossMacro.Cli.Tests;

public sealed class ScreenshotCommandHandlerTests
{
    [Fact]
    public void Constructor_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new ScreenshotCommandHandler(screenshotCliService: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesOptionsAndCancellation()
    {
        var screenshotCliService = Substitute.For<IScreenshotCliService>();
        var options = new ScreenshotCliOptions(
            ScreenshotCliAction.Capture,
            OutputPath: "shot.png",
            RegionX: 1,
            RegionY: 2,
            RegionWidth: 2,
            RegionHeight: 1,
            JsonOutput: true);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        _ = screenshotCliService.ExecuteAsync(options, cancellationToken)
            .Returns(CliCommandExecutionResult.Ok("Screenshot captured."));

        var handler = new ScreenshotCommandHandler(screenshotCliService);
        var result = await handler.ExecuteAsync(options, cancellationToken);

        Assert.True(result.Success);
        _ = await screenshotCliService.Received(1).ExecuteAsync(options, cancellationToken);
    }
}
