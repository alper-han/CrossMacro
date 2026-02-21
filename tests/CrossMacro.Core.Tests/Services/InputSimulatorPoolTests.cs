using CrossMacro.Core.Services;
using FluentAssertions;

namespace CrossMacro.Core.Tests.Services;

public class InputSimulatorPoolTests
{
    private readonly List<FakeInputSimulator> _created = [];
    private readonly InputSimulatorPool _pool;

    public InputSimulatorPoolTests()
    {
        _pool = new InputSimulatorPool(() =>
        {
            var simulator = new FakeInputSimulator();
            _created.Add(simulator);
            return simulator;
        });
    }

    [Fact]
    public void Acquire_WhenNoWarmDevice_CreatesAndInitializesNewDevice()
    {
        // Act
        var acquired = _pool.Acquire(1920, 1080);

        // Assert
        acquired.Should().BeOfType<FakeInputSimulator>();
        _created.Should().HaveCount(1);
        _created[0].InitializeCalls.Should().ContainSingle();
        _created[0].InitializeCalls[0].Should().Be((1920, 1080));
    }

    [Fact]
    public async Task WarmUpAsync_CreatesWarmDevice()
    {
        // Act
        await _pool.WarmUpAsync();

        // Assert
        _pool.HasWarmDevice.Should().BeTrue();
    }

    [Fact]
    public void Release_DisposesReturnedDevice()
    {
        // Arrange
        var acquired = (FakeInputSimulator)_pool.Acquire(0, 0);

        // Act
        _pool.Release(acquired);

        // Assert
        acquired.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Act
        var act = () =>
        {
            _pool.Dispose();
            _pool.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    private sealed class FakeInputSimulator : IInputSimulator
    {
        public List<(int Width, int Height)> InitializeCalls { get; } = [];
        public bool IsDisposed { get; private set; }

        public string ProviderName => "Fake";
        public bool IsSupported => true;

        public void Initialize(int screenWidth = 0, int screenHeight = 0)
        {
            InitializeCalls.Add((screenWidth, screenHeight));
        }

        public void MoveAbsolute(int x, int y) { }
        public void MoveRelative(int dx, int dy) { }
        public void MouseButton(int button, bool pressed) { }
        public void Scroll(int delta, bool isHorizontal = false) { }
        public void KeyPress(int keyCode, bool pressed) { }
        public void Sync() { }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
