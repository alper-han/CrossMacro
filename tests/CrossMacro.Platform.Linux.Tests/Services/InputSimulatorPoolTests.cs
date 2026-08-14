
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
    public void Release_ReturnsCompatibleDeviceToWarmPool()
    {
        var acquired = (FakeInputSimulator)_pool.Acquire(0, 0);

        _pool.Release(acquired);
        var reused = _pool.Acquire(0, 0);

        _ = reused.Should().BeSameAs(acquired);
        _ = acquired.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseAsync_ReturnsCompatibleDeviceToWarmPool()
    {
        var acquired = await _pool.AcquireAsync(1920, 1080);
        _pool.Release(acquired);
        var reused = await _pool.AcquireAsync(1920, 1080);

        _ = reused.Should().BeSameAs(acquired);
    }

    [Fact]
    public async Task AcquireAsync_WhenReusingRefreshableDevice_RefreshesItsLease()
    {
        var acquired = (FakeInputSimulator)await _pool.AcquireAsync(1920, 1080);
        _pool.Release(acquired);

        var reused = await _pool.AcquireAsync(1920, 1080);

        _ = reused.Should().BeSameAs(acquired);
        _ = acquired.LeaseRefreshCalls.Should().ContainSingle().Which.Should().Be((1920, 1080));
    }

    [Fact]
    public async Task AcquireAsync_WhenRefreshingWarmDeviceFails_DisposesIt()
    {
        var acquired = (FakeInputSimulator)await _pool.AcquireAsync(1920, 1080);
        _pool.Release(acquired);
        acquired.LeaseRefreshException = new InvalidOperationException("refresh failed");

        var act = () => _pool.AcquireAsync(1920, 1080);

        _ = await act.Should().ThrowAsync<InvalidOperationException>();
        _ = acquired.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Release_WhenResolutionChanges_DisposesStaleAbsoluteDevice()
    {
        var acquired = (FakeInputSimulator)_pool.Acquire(1920, 1080);

        _pool.Release(acquired);
        _ = _pool.Acquire(2560, 1440);

        _ = acquired.DisposeCalls.Should().Be(1);
    }

    [Fact]
    public void Dispose_DisposesLeasedDevice()
    {
        var acquired = (FakeInputSimulator)_pool.Acquire(0, 0);

        _pool.Dispose();

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

    [Fact]
    public async Task WarmUpAsync_WhenCancellationIsRequested_DoesNotCreateADevice()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await _pool.WarmUpAsync(cancellationToken: cancellation.Token);

        _ = _created.Should().BeEmpty();
        _ = _pool.HasWarmDevice.Should().BeFalse();
    }

    private sealed class FakeInputSimulator : IInputSimulator, IInputSimulatorLeaseRefresher
    {
        public List<(int Width, int Height)> InitializeCalls { get; } = [];
        public List<(int Width, int Height)> LeaseRefreshCalls { get; } = [];
        public Exception? LeaseRefreshException { get; set; }
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

        public Task RefreshLeaseAsync(int screenWidth, int screenHeight, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LeaseRefreshCalls.Add((screenWidth, screenHeight));
            return LeaseRefreshException is { } exception
                ? Task.FromException(exception)
                : Task.CompletedTask;
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
