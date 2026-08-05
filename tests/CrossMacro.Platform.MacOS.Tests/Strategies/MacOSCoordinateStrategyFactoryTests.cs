
namespace CrossMacro.Platform.MacOS.Tests.Strategies;

public sealed class MacOSCoordinateStrategyFactoryTests
{
    [Fact]
    public void Create_WhenAbsoluteRequested_ReturnsMacOSAbsoluteStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory();

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false);

        _ = Assert.IsType<MacOSAbsoluteCoordinateStrategy>(strategy);
    }

    [Fact]
    public void Create_WhenForceRelativeRequested_ReturnsMacOSRelativeStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory();

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: true, skipInitialZero: false);

        _ = Assert.IsType<MacOSRelativeCoordinateStrategy>(strategy);
    }

    [Fact]
    public void Create_WhenRelativeRequested_ReturnsMacOSRelativeStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory();

        var strategy = factory.Create(useAbsoluteCoordinates: false, forceRelative: false, skipInitialZero: false);

        _ = Assert.IsType<MacOSRelativeCoordinateStrategy>(strategy);
    }

    [Fact]
    public void Create_WhenAbsoluteRequestedWithSkipInitialZero_ReturnsMacOSAbsoluteStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory();

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

    private sealed class StubMousePositionProvider((int X, int Y)? position) : IMousePositionProvider
    {
        private readonly (int X, int Y)? _position = position;

        public int AbsolutePositionQueries { get; private set; }
        public string ProviderName => "test";
        public bool IsSupported => true;

        public Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            AbsolutePositionQueries++;
            return Task.FromResult(_position);
        }

        public Task<(int Width, int Height)?> GetScreenResolutionAsync() => Task.FromResult<(int Width, int Height)?>(null);

        public void Dispose() { /* Test provider has no resources. */ }
    }
}
