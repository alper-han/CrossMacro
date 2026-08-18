namespace CrossMacro.Cli.Tests;

public sealed class QuickSetupCliServiceTests
{
    [Fact]
    public async Task RunAsync_UsesFlatpakProviderWhenApplicable()
    {
        var flatpak = Substitute.For<IFlatpakQuickSetupService>();
        _ = flatpak.IsApplicable().Returns(true);
        _ = flatpak.RunAsync(Arg.Any<CancellationToken>())
            .Returns(new QuickSetupResult(true, "flatpak setup complete"));

        var appImage = Substitute.For<IAppImageQuickSetupService>();
        var service = new QuickSetupCliService([flatpak], [appImage]);

        var result = await service.RunAsync(CancellationToken.None);

        Assert.True(result.Applicable);
        Assert.Equal("flatpak", result.Provider);
        Assert.True(result.Result.Success);
        _ = appImage.DidNotReceive().IsApplicable();
    }

    [Fact]
    public async Task RunAsync_UsesAppImageProviderWhenFlatpakIsNotApplicable()
    {
        var flatpak = Substitute.For<IFlatpakQuickSetupService>();
        _ = flatpak.IsApplicable().Returns(false);
        var appImage = Substitute.For<IAppImageQuickSetupService>();
        _ = appImage.IsApplicable().Returns(true);
        _ = appImage.RunAsync(Arg.Any<CancellationToken>())
            .Returns(new QuickSetupResult(true, "appimage setup complete"));
        var service = new QuickSetupCliService([flatpak], [appImage]);

        var result = await service.RunAsync(CancellationToken.None);

        Assert.True(result.Applicable);
        Assert.Equal("appimage", result.Provider);
        Assert.True(result.Result.Success);
    }

    [Fact]
    public async Task RunAsync_ReturnsNotApplicableOutsideSupportedPackageSessions()
    {
        var service = new QuickSetupCliService([], []);

        var result = await service.RunAsync(CancellationToken.None);

        Assert.False(result.Applicable);
        Assert.Equal("none", result.Provider);
        Assert.False(result.Result.Success);
    }
}
