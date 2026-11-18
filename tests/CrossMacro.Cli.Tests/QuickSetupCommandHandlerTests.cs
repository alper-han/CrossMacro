namespace CrossMacro.Cli.Tests;

public sealed class QuickSetupCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenSetupSucceeds_ReturnsProviderData()
    {
        var service = Substitute.For<IQuickSetupCommandService>();
        _ = service.RunAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(returnThis: new QuickSetupCommandResult(
                Applicable: true,
                Provider: "flatpak",
                Result: new QuickSetupResult(Outcome: QuickSetupOutcome.Succeeded, Message: "Quick setup completed.")));
        var handler = new QuickSetupCommandHandler(service);

        var result = await handler.ExecuteAsync(new QuickSetupOptions(), CancellationToken.None);

        Assert.True(result.Success);
        var data = Assert.IsType<QuickSetupCommandData>(result.Data);
        Assert.Equal("flatpak", data.Provider);
        Assert.True(data.Applicable);
        Assert.True(data.Applied);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSetupIsNotApplicable_ReturnsEnvironmentError()
    {
        var service = Substitute.For<IQuickSetupCommandService>();
        _ = service.RunAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(returnThis: new QuickSetupCommandResult(
                Applicable: false,
                Provider: "none",
                Result: new QuickSetupResult(Outcome: QuickSetupOutcome.Failed, Message: "not applicable")));
        var handler = new QuickSetupCommandHandler(service);

        var result = await handler.ExecuteAsync(new QuickSetupOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.EnvironmentError, result.Outcome);
        Assert.Contains("not applicable", result.Errors, StringComparer.Ordinal);
        var data = Assert.IsType<QuickSetupCommandData>(result.Data);
        Assert.Equal("none", data.Provider);
        Assert.False(data.Applicable);
        Assert.False(data.Applied);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSetupFails_ReturnsEnvironmentError()
    {
        var service = Substitute.For<IQuickSetupCommandService>();
        _ = service.RunAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(returnThis: new QuickSetupCommandResult(
                Applicable: true,
                Provider: "appimage",
                Result: new QuickSetupResult(Outcome: QuickSetupOutcome.AuthorizationDenied, Message: "authorization denied")));
        var handler = new QuickSetupCommandHandler(service);

        var result = await handler.ExecuteAsync(new QuickSetupOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.EnvironmentError, result.Outcome);
        Assert.Contains("authorization denied", result.Errors, StringComparer.Ordinal);
        var data = Assert.IsType<QuickSetupCommandData>(result.Data);
        Assert.Equal("appimage", data.Provider);
        Assert.True(data.Applicable);
        Assert.False(data.Applied);
    }
}
