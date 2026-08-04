using CrossMacro.Core.Services.Playback;
using CrossMacro.Infrastructure.Services.Playback;

namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class CosmicAbsoluteInputSimulatorTests
{
    [Fact]
    public async Task MoveAbsolute_OnActiveOutput_MapsDesktopAxisToOutputLocalAxis()
    {
        var backend = new RecordingInputSimulator();
        using var provider = CreateProvider((3000, 600),
        [
            new ScreenRect(0, 0, 2560, 1440),
            new ScreenRect(2560, 0, 2560, 1440),
        ]);
        using var simulator = new CosmicAbsoluteInputSimulator(backend, provider, provider);
        await simulator.InitializeAsync(5120, 1440, CancellationToken.None);

        simulator.MoveAbsolute(4160, 500);

        Assert.True(simulator.SupportsAbsoluteCoordinates);
        Assert.Equal([new SimulationCall("absolute", 3200, 500)], backend.Calls);
    }

    [Fact]
    public async Task MoveAbsolute_OnOtherOutput_CrossesSharedEdgeBeforeLocalAbsoluteMove()
    {
        var backend = new RecordingInputSimulator();
        using var provider = CreateProvider((3419, 909),
        [
            new ScreenRect(0, 0, 2560, 1440),
            new ScreenRect(2560, 0, 2560, 1440),
        ]);
        using var simulator = new CosmicAbsoluteInputSimulator(backend, provider, provider);
        await simulator.InitializeAsync(5120, 1440, CancellationToken.None);

        simulator.MoveAbsolute(1600, 500);

        Assert.Equal(
        [
            new SimulationCall("absolute", 0, 719),
            new SimulationCall("relative", -8, 0),
            new SimulationCall("absolute", 3200, 500),
        ], backend.Calls);
    }

    [Fact]
    public async Task MoveAbsolute_AcrossThreeOutputs_UsesAdjacentRoute()
    {
        var backend = new RecordingInputSimulator();
        using var provider = CreateProvider((25, 50),
        [
            new ScreenRect(0, 0, 100, 100),
            new ScreenRect(100, 0, 100, 100),
            new ScreenRect(200, 0, 100, 100),
        ]);
        using var simulator = new CosmicAbsoluteInputSimulator(backend, provider, provider);
        await simulator.InitializeAsync(300, 100, CancellationToken.None);

        simulator.MoveAbsolute(250, 50);

        Assert.Equal(
        [
            new SimulationCall("absolute", 297, 49),
            new SimulationCall("relative", 8, 0),
            new SimulationCall("absolute", 297, 49),
            new SimulationCall("relative", 8, 0),
            new SimulationCall("absolute", 150, 50),
        ], backend.Calls);
    }

    [Fact]
    public async Task MoveAbsolute_WithNegativeDesktopOrigin_TranslatesZeroBasedInput()
    {
        var backend = new RecordingInputSimulator();
        using var provider = CreateProvider((-1200, 400),
        [
            new ScreenRect(-1920, 0, 1920, 1080),
            new ScreenRect(0, 0, 2560, 1440),
        ]);
        using var simulator = new CosmicAbsoluteInputSimulator(backend, provider, provider);
        await simulator.InitializeAsync(4480, 1440, CancellationToken.None);

        simulator.MoveAbsolute(960, 540);

        Assert.Equal([new SimulationCall("absolute", 2240, 720)], backend.Calls);
    }

    [Fact]
    public async Task MoveRelative_InvalidatesActiveOutputAndRefreshesFromProvider()
    {
        var backend = new RecordingInputSimulator();
        using var provider = CreateProvider((100, 100),
        [
            new ScreenRect(0, 0, 2560, 1440),
            new ScreenRect(2560, 0, 2560, 1440),
        ]);
        using var simulator = new CosmicAbsoluteInputSimulator(backend, provider, provider);
        await simulator.InitializeAsync(5120, 1440, CancellationToken.None);

        simulator.MoveRelative(20, 0);
        provider.Position = (3000, 100);
        simulator.MoveAbsolute(3560, 500);

        Assert.Equal(
        [
            new SimulationCall("relative", 20, 0),
            new SimulationCall("absolute", 2000, 500),
        ], backend.Calls);
    }

    [Fact]
    public async Task LogicalRelativePlayback_OnCosmic_UsesOutputMappedAbsoluteTransport()
    {
        var backend = new RecordingInputSimulator();
        using var provider = CreateProvider((3000, 600),
        [
            new ScreenRect(0, 0, 2560, 1440),
            new ScreenRect(2560, 0, 2560, 1440),
        ]);
        using var simulator = new CosmicAbsoluteInputSimulator(backend, provider, provider);
        await simulator.InitializeAsync(5120, 1440, CancellationToken.None);
        var coordinator = new DefaultPlaybackCoordinator();
        coordinator.UpdatePosition(3000, 600);
        using var executor = new MacroEventExecutor(
            simulator,
            Substitute.For<IButtonStateTracker>(),
            Substitute.For<IKeyStateTracker>(),
            Substitute.For<IPlaybackMouseButtonMapper>(),
            coordinator);
        executor.Initialize(new ScreenRect(0, 0, 5120, 1440));

        executor.Execute(
            new MacroEvent { Type = EventType.MouseMove, X = 100, Y = -100 },
            MouseCoordinateMode.Relative,
            MouseCoordinateSpace.LogicalDesktop);

        Assert.Equal([new SimulationCall("absolute", 1080, 500)], backend.Calls);
        Assert.Equal(3100, coordinator.CurrentX);
        Assert.Equal(500, coordinator.CurrentY);
    }

    [Fact]
    public async Task InitializeRelativeOnly_LeavesAbsoluteCapabilityDisabled()
    {
        var backend = new RecordingInputSimulator();
        using var provider = CreateProvider((100, 100), [new ScreenRect(0, 0, 1920, 1080)]);
        using var simulator = new CosmicAbsoluteInputSimulator(backend, provider, provider);

        await simulator.InitializeAsync(cancellationToken: CancellationToken.None);
        simulator.MoveRelative(4, -3);

        Assert.False(simulator.SupportsAbsoluteCoordinates);
        Assert.Equal([new SimulationCall("relative", 4, -3)], backend.Calls);
    }

    [Fact]
    public async Task Initialize_WithMultipleOutputsAndResolutionOnlyProvider_DisablesAbsoluteCapability()
    {
        var backend = new RecordingInputSimulator();
        using var provider = CreateProvider((100, 100),
        [
            new ScreenRect(0, 0, 1920, 1080),
            new ScreenRect(1920, 0, 1920, 1080),
        ]);
        provider.SupportsAbsolutePosition = false;
        using var simulator = new CosmicAbsoluteInputSimulator(backend, provider, provider);

        await simulator.InitializeAsync(3840, 1080, CancellationToken.None);

        Assert.False(simulator.SupportsAbsoluteCoordinates);
        _ = Assert.Throws<InvalidOperationException>(() => simulator.MoveAbsolute(2400, 500));
    }

    private static FakeDesktopProvider CreateProvider(
        (int X, int Y) position,
        IReadOnlyList<ScreenRect> outputs) => new(position, outputs);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct SimulationCall(string Kind, int X, int Y);

    private sealed class RecordingInputSimulator : IInputSimulator, IInputSimulatorCapabilities
    {
        public List<SimulationCall> Calls { get; } = [];
        public string ProviderName => "recording";
        public bool IsSupported => true;
        public bool SupportsAbsoluteCoordinates { get; private set; }

        public void Initialize(int screenWidth = 0, int screenHeight = 0)
        {
            SupportsAbsoluteCoordinates = screenWidth > 0 && screenHeight > 0;
        }

        public Task InitializeAsync(
            int screenWidth = 0,
            int screenHeight = 0,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Initialize(screenWidth, screenHeight);
            return Task.CompletedTask;
        }

        public void MoveAbsolute(int x, int y) => Calls.Add(new SimulationCall("absolute", x, y));
        public void MoveRelative(int dx, int dy) => Calls.Add(new SimulationCall("relative", dx, dy));
        public void MouseButton(int button, bool pressed) { }
        public void Scroll(int delta, bool isHorizontal = false) { }
        public void KeyPress(int keyCode, bool pressed) { }
        public void Sync() { }
        public void Dispose() { }
    }

    private sealed class FakeDesktopProvider(
        (int X, int Y) position,
        IReadOnlyList<ScreenRect> outputs) :
        IMousePositionProvider,
        IOutputTopologyProvider
    {
        private readonly IReadOnlyList<ScreenRect> _outputs = outputs;

        public (int X, int Y) Position { get; set; } = position;
        public string ProviderName => "fake desktop";
        public bool IsSupported => true;
        public bool SupportsAbsolutePosition { get; set; } = true;

        public Task<(int X, int Y)?> GetAbsolutePositionAsync() =>
            Task.FromResult<(int X, int Y)?>(Position);

        public Task<(int Width, int Height)?> GetScreenResolutionAsync()
        {
            var bounds = ComputeBounds();
            return Task.FromResult<(int Width, int Height)?>((bounds.Width, bounds.Height));
        }

        public Task<ScreenRect?> GetDesktopBoundsAsync() =>
            Task.FromResult<ScreenRect?>(ComputeBounds());

        public Task<IReadOnlyList<ScreenRect>> GetOutputBoundsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_outputs);
        }

        public void Dispose() { }

        private ScreenRect ComputeBounds()
        {
            int x = _outputs.Min(static output => output.X);
            int y = _outputs.Min(static output => output.Y);
            int right = _outputs.Max(static output => output.Right);
            int bottom = _outputs.Max(static output => output.Bottom);
            return new ScreenRect(x, y, right - x, bottom - y);
        }
    }
}
