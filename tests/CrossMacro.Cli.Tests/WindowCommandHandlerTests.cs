namespace CrossMacro.Cli.Tests;

public sealed class WindowCommandHandlerTests
{
    [Fact]
    public void Constructor_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new WindowCommandHandler(windowCliService: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesOptionsAndCancellation()
    {
        var service = Substitute.For<IWindowCliService>();
        var options = new WindowCliOptions(
            WindowCliAction.Resize,
            Selector: new WindowSelector(WindowSelectorKind.Class, "code"),
            X: 10,
            Y: 20,
            Width: 1200,
            Height: 800,
            TimeoutMs: 1500,
            WorkspaceName: "dev",
            JsonOutput: true);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        _ = service.ExecuteAsync(options, cancellationToken)
            .Returns(CliCommandExecutionResult.Ok("Window resized."));

        var handler = new WindowCommandHandler(service);
        var result = await handler.ExecuteAsync(options, cancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Window resized.", result.Message);
        _ = await service.Received(1).ExecuteAsync(options, cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceFails_PropagatesFailure()
    {
        var service = Substitute.For<IWindowCliService>();
        var options = new WindowCliOptions(WindowCliAction.Focus);
        _ = service.ExecuteAsync(options, Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Window operation unavailable.",
                errors: ["Window backend is unsupported."]));

        var handler = new WindowCommandHandler(service);
        var result = await handler.ExecuteAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Equal("Window operation unavailable.", result.Message);
        Assert.Equal(["Window backend is unsupported."], result.Errors);
    }
}
