namespace CrossMacro.Cli.Tests;

public sealed class QuickSetupCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenSetupSucceeds_ReturnsProviderData()
    {
        var service = Substitute.For<IQuickSetupCliService>();
        _ = service.RunAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(returnThis: new QuickSetupCliResult(
                Applicable: true,
                Provider: "flatpak",
                Result: new QuickSetupResult(Success: true, Message: "Quick setup completed.")));
        var handler = new QuickSetupCommandHandler(service);

        var result = await handler.ExecuteAsync(new QuickSetupCliOptions(), CancellationToken.None);

        Assert.True(result.Success);
        var data = Assert.IsType<QuickSetupCommandData>(result.Data);
        Assert.Equal("flatpak", data.Provider);
        Assert.True(data.Applicable);
        Assert.True(data.Applied);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSetupIsNotApplicable_ReturnsEnvironmentError()
    {
        var service = Substitute.For<IQuickSetupCliService>();
        _ = service.RunAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(returnThis: new QuickSetupCliResult(
                Applicable: false,
                Provider: "none",
                Result: new QuickSetupResult(Success: false, Message: "not applicable")));
        var handler = new QuickSetupCommandHandler(service);

        var result = await handler.ExecuteAsync(new QuickSetupCliOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("not applicable", result.Errors, StringComparer.Ordinal);
        var data = Assert.IsType<QuickSetupCommandData>(result.Data);
        Assert.Equal("none", data.Provider);
        Assert.False(data.Applicable);
        Assert.False(data.Applied);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSetupFails_ReturnsEnvironmentError()
    {
        var service = Substitute.For<IQuickSetupCliService>();
        _ = service.RunAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(returnThis: new QuickSetupCliResult(
                Applicable: true,
                Provider: "appimage",
                Result: new QuickSetupResult(Success: false, Message: "authorization denied")));
        var handler = new QuickSetupCommandHandler(service);

        var result = await handler.ExecuteAsync(new QuickSetupCliOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("authorization denied", result.Errors, StringComparer.Ordinal);
        var data = Assert.IsType<QuickSetupCommandData>(result.Data);
        Assert.Equal("appimage", data.Provider);
        Assert.True(data.Applicable);
        Assert.False(data.Applied);
    }
}
