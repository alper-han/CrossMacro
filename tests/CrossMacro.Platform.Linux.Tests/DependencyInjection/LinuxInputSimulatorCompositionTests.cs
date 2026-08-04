namespace CrossMacro.Platform.Linux.Tests.DependencyInjection;

public sealed class LinuxInputSimulatorCompositionTests
{
    [Fact]
    public void ApplyCompositorInputMapping_ForCosmicWithOutputTopology_WrapsSimulator()
    {
        var simulator = Substitute.For<IInputSimulator>();
        using var positionProvider = new TestPositionProvider();

        using var result = LinuxSimulatorFactory.ApplyCompositorInputMapping(
            simulator,
            CompositorType.COSMIC,
            positionProvider);

        _ = Assert.IsType<CosmicAbsoluteInputSimulator>(result);
    }

    [Theory]
    [InlineData(CompositorType.KDE)]
    [InlineData(CompositorType.GNOME)]
    [InlineData(CompositorType.HYPRLAND)]
    public void ApplyCompositorInputMapping_ForOtherWaylandCompositors_LeavesSimulatorUntouched(
        CompositorType compositor)
    {
        var simulator = Substitute.For<IInputSimulator>();
        bool positionProviderRequested = false;

        var result = LinuxSimulatorFactory.ApplyCompositorInputMapping(
            simulator,
            compositor,
            new TrackingPositionProvider(() => positionProviderRequested = true));

        Assert.Same(simulator, result);
        Assert.False(positionProviderRequested);
    }

    [Fact]
    public void ApplyCompositorInputMapping_ForCosmicWithoutOutputTopology_LeavesSimulatorUntouched()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var positionProvider = Substitute.For<IMousePositionProvider>();

        var result = LinuxSimulatorFactory.ApplyCompositorInputMapping(
            simulator,
            CompositorType.COSMIC,
            positionProvider);

        Assert.Same(simulator, result);
    }

    [Fact]
    public void ApplyCompositorInputMapping_ForUnavailableSimulator_DoesNotResolvePositionProvider()
    {
        var simulator = new UnavailableInputSimulator("unavailable");
        bool positionProviderRequested = false;

        var result = LinuxSimulatorFactory.ApplyCompositorInputMapping(
            simulator,
            CompositorType.COSMIC,
            new TrackingPositionProvider(() => positionProviderRequested = true));

        Assert.Same(simulator, result);
        Assert.False(positionProviderRequested);
    }

    private sealed class TestPositionProvider : IMousePositionProvider, IOutputTopologyProvider
    {
        public string ProviderName => "test position provider";
        public bool IsSupported => true;

        public Task<(int X, int Y)?> GetAbsolutePositionAsync() =>
            Task.FromResult<(int X, int Y)?>((0, 0));

        public Task<(int Width, int Height)?> GetScreenResolutionAsync() =>
            Task.FromResult<(int Width, int Height)?>((1920, 1080));

        public Task<IReadOnlyList<ScreenRect>> GetOutputBoundsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ScreenRect>>(
                [new ScreenRect(0, 0, 1920, 1080)]);
        }

        public void Dispose() { }
    }

    private sealed class TrackingPositionProvider(Action onAccess) : IMousePositionProvider
    {
        public string ProviderName => "tracking position provider";
        public bool IsSupported => true;

        public Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            onAccess();
            return Task.FromResult<(int X, int Y)?>(null);
        }

        public Task<(int Width, int Height)?> GetScreenResolutionAsync() =>
            Task.FromResult<(int Width, int Height)?>(null);

        public void Dispose() { }
    }
}
