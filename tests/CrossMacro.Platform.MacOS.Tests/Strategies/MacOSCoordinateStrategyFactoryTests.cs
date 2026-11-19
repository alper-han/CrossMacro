using CrossMacro.Platform.Abstractions.Recording.Strategies;
using CrossMacro.Platform.MacOS.Strategies;

namespace CrossMacro.Platform.MacOS.Tests.Strategies;

public sealed class MacOSCoordinateStrategyFactoryTests
{
    [Fact]
    public void Create_WhenAbsoluteRequested_ReturnsMacOSAbsoluteStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory(new StubMousePositionProvider(position: null));

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false);

        _ = Assert.IsType<MacOSAbsoluteCoordinateStrategy>(strategy);
    }

    [Fact]
    public void Create_WhenForceRelativeRequested_ReturnsRawRelativeStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory(new StubMousePositionProvider(position: null));

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: true, skipInitialZero: false);

        var rawStrategy = Assert.IsType<RelativeCoordinateStrategy>(strategy);
        Assert.False(rawStrategy.ProducesLogicalCoordinates);
    }

    [Fact]
    public void Create_WhenRelativeRequested_ReturnsRawRelativeStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory(new StubMousePositionProvider(position: null));

        var strategy = factory.Create(useAbsoluteCoordinates: false, forceRelative: false, skipInitialZero: false);

        var rawStrategy = Assert.IsType<RelativeCoordinateStrategy>(strategy);
        Assert.False(rawStrategy.ProducesLogicalCoordinates);
    }

    [Fact]
    public void Create_WhenLogicalRelativeRequested_ReturnsMacOSLogicalRelativeStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory(new StubMousePositionProvider(position: null));

        var strategy = factory.Create(
            useAbsoluteCoordinates: true,
            forceRelative: true,
            skipInitialZero: false,
            useLogicalRelativeCoordinates: true);

        var logicalStrategy = Assert.IsType<MacOSRelativeCoordinateStrategy>(strategy);
        Assert.True(logicalStrategy.ProducesLogicalCoordinates);
    }

    [Fact]
    public void Create_WhenAbsoluteRequestedWithSkipInitialZero_ReturnsMacOSAbsoluteStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory(new StubMousePositionProvider(position: null));

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: true);

        _ = Assert.IsType<MacOSAbsoluteCoordinateStrategy>(strategy);
    }

    [Fact]
    public async Task Create_WhenProviderIsInjected_UsesProviderForAbsoluteInitialization()
    {
        using var provider = new StubMousePositionProvider((321, 654));
        var factory = new MacOSCoordinateStrategyFactory(provider);
        using var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false);

        await strategy.InitializeAsync(CancellationToken.None);
        var sample = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.Key,
            Code = InputEventCode.KEY_A,
            Value = 1,
        });

        Assert.Equal(CoordinateSample.Create(321, 654), sample);
        Assert.Equal(1, provider.AbsolutePositionQueries);
    }

    [Fact]
    public async Task Create_WhenLogicalRelativeProviderCompletesAsynchronously_InitializesWithoutBlocking()
    {
        using var provider = new DelayedMousePositionProvider((321, 654));
        var factory = new MacOSCoordinateStrategyFactory(provider);
        using var strategy = factory.Create(
            useAbsoluteCoordinates: true,
            forceRelative: true,
            skipInitialZero: false,
            useLogicalRelativeCoordinates: true);

        var initializeTask = strategy.InitializeAsync(CancellationToken.None);
        Assert.False(initializeTask.IsCompleted);

        provider.CompletePositionRequest();
        await initializeTask;
        Assert.Equal(1, provider.AbsolutePositionQueries);
    }
}
