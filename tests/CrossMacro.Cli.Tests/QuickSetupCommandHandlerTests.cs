namespace CrossMacro.Cli.Tests;

public sealed class QuickSetupCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenSetupSucceeds_ReturnsProviderData()
    {
        var service = Substitute.For<IQuickSetupCliService>();
        _ = service.RunAsync(Arg.Any<CancellationToken>())
            .Returns(new QuickSetupCliResult(true, "flatpak", new QuickSetupResult(true, "Quick setup completed.")));
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
        _ = service.RunAsync(Arg.Any<CancellationToken>())
            .Returns(new QuickSetupCliResult(false, "none", new QuickSetupResult(false, "not applicable")));
        var handler = new QuickSetupCommandHandler(service);

        var result = await handler.ExecuteAsync(new QuickSetupCliOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("not applicable", result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSetupFails_ReturnsEnvironmentError()
    {
        var service = Substitute.For<IQuickSetupCliService>();
        _ = service.RunAsync(Arg.Any<CancellationToken>())
            .Returns(new QuickSetupCliResult(true, "appimage", new QuickSetupResult(false, "authorization denied")));
        var handler = new QuickSetupCommandHandler(service);

        var result = await handler.ExecuteAsync(new QuickSetupCliOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("authorization denied", result.Errors, StringComparer.Ordinal);
    }
}
