
namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class InputSimulatorPoolTests : IDisposable
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

    public void Dispose() => _pool.Dispose();

    [Fact]
    public void Acquire_WhenNoWarmDevice_CreatesAndInitializesNewDevice()
    {
        // Act
        var acquired = _pool.Acquire(1920, 1080);

        // Assert
        _ = acquired.Should().BeOfType<FakeInputSimulator>();
        _ = _created.Should().HaveCount(1);
        _ = _created[0].InitializeCalls.Should().ContainSingle();
        _ = _created[0].InitializeCalls[0].Should().Be((1920, 1080));
    }

    [Fact]
    public async Task WarmUpAsync_CreatesWarmDevice()
    {
        // Act
        await _pool.WarmUpAsync();

        // Assert
        _ = _pool.HasWarmDevice.Should().BeTrue();
    }

    [Fact]
    public void Release_DisposesReturnedDevice()
    {
        // Arrange
        var acquired = (FakeInputSimulator)_pool.Acquire(0, 0);

        // Act
        _pool.Release(acquired);

        // Assert
        _ = acquired.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task Release_TracksReplacementWorkUntilItSettles()
    {
        var acquired = _pool.Acquire(0, 0);
        _pool.Release(acquired);

        await _pool.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        _ = _pool.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void Release_WhenCalledTwice_DisposesTheLeaseOnlyOnce()
    {
        var acquired = (FakeInputSimulator)_pool.Acquire(0, 0);

        _pool.Release(acquired);
        _pool.Release(acquired);

        _ = acquired.DisposeCalls.Should().Be(1);
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
        _ = act.Should().NotThrow();
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledAfterWarmup()
    {
        await _pool.WarmUpAsync();

        await _pool.DisposeAsync();

        _ = _pool.HasWarmDevice.Should().BeFalse();
        await _pool.WarmUpAsync();
        _ = _pool.HasWarmDevice.Should().BeFalse();
    }

    [Fact]
    public async Task WarmUpAsync_WhenDisposedConcurrently_DoesNotThrow()
    {
        var warmUpTask = _pool.WarmUpAsync(1920, 1080);
        _pool.Dispose();

        var act = async () => await warmUpTask;
        _ = await act.Should().NotThrowAsync();
    }

    private sealed class FakeInputSimulator : IInputSimulator
    {
        public List<(int Width, int Height)> InitializeCalls { get; } = [];
        public bool IsDisposed { get; private set; }
        public int DisposeCalls { get; private set; }

        public string ProviderName => "Fake";
        public bool IsSupported => true;

        public void Initialize(int screenWidth = 0, int screenHeight = 0)
        {
            InitializeCalls.Add((screenWidth, screenHeight));
        }

        public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Initialize(screenWidth, screenHeight);
            return Task.CompletedTask;
        }

        public void MoveAbsolute(int x, int y) { }
        public void MoveRelative(int dx, int dy) { }
        public void MouseButton(int button, bool pressed) { }
        public void Scroll(int delta, bool isHorizontal = false) { }
        public void KeyPress(int keyCode, bool pressed) { }
        public void Sync() { }

        public void Dispose()
        {
            DisposeCalls++;
            IsDisposed = true;
        }
    }
}
