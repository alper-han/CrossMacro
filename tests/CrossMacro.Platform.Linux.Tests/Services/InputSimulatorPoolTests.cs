
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
        var acquired = _pool.Acquire(1920, 1080);

        _ = Assert.IsType<FakeInputSimulator>(acquired);
        _ = Assert.Single(_created);
        _ = Assert.Single(_created[0].InitializeCalls);
        Assert.Equal((1920, 1080), _created[0].InitializeCalls[0]);
    }

    [Fact]
    public async Task WarmUpAsync_CreatesWarmDevice()
    {
        await _pool.WarmUpAsync(cancellationToken: CancellationToken.None);

        Assert.True(_pool.HasWarmDevice);
    }

    [Fact]
    public void Release_ReturnsCompatibleDeviceToWarmPool()
    {
        var acquired = (FakeInputSimulator)_pool.Acquire(0, 0);

        _pool.Release(acquired);
        var reused = _pool.Acquire(0, 0);

        Assert.Same(acquired, reused);
        Assert.False(acquired.IsDisposed);
    }

    [Fact]
    public async Task ReleaseAsync_ReturnsCompatibleDeviceToWarmPool()
    {
        var acquired = await _pool.AcquireAsync(1920, 1080, CancellationToken.None);
        _pool.Release(acquired);
        var reused = await _pool.AcquireAsync(1920, 1080, CancellationToken.None);

        Assert.Same(acquired, reused);
    }

    [Fact]
    public async Task AcquireAsync_WhenReusingRefreshableDevice_RefreshesItsLease()
    {
        var acquired = (FakeInputSimulator)await _pool.AcquireAsync(1920, 1080, CancellationToken.None);
        _pool.Release(acquired);

        var reused = await _pool.AcquireAsync(1920, 1080, CancellationToken.None);

        Assert.Same(acquired, reused);
        Assert.Equal((1920, 1080), Assert.Single(acquired.LeaseRefreshCalls));
    }

    [Fact]
    public async Task AcquireAsync_WhenRefreshingWarmDeviceFails_DisposesIt()
    {
        var acquired = (FakeInputSimulator)await _pool.AcquireAsync(1920, 1080, CancellationToken.None);
        _pool.Release(acquired);
        acquired.LeaseRefreshException = new InvalidOperationException("refresh failed");

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _pool.AcquireAsync(1920, 1080, CancellationToken.None));
        Assert.True(acquired.IsDisposed);
    }

    [Fact]
    public void Release_WhenResolutionChanges_DisposesStaleAbsoluteDevice()
    {
        var acquired = (FakeInputSimulator)_pool.Acquire(1920, 1080);

        _pool.Release(acquired);
        _ = _pool.Acquire(2560, 1440);

        Assert.Equal(1, acquired.DisposeCalls);
    }

    [Fact]
    public void Dispose_DisposesLeasedDevice()
    {
        var acquired = (FakeInputSimulator)_pool.Acquire(0, 0);

        _pool.Dispose();

        Assert.Equal(1, acquired.DisposeCalls);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _pool.Dispose();
        _pool.Dispose();

        Assert.False(_pool.HasWarmDevice);
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledAfterWarmup()
    {
        await _pool.WarmUpAsync(cancellationToken: CancellationToken.None);

        await _pool.DisposeAsync();

        Assert.False(_pool.HasWarmDevice);
        await _pool.WarmUpAsync(cancellationToken: CancellationToken.None);
        Assert.False(_pool.HasWarmDevice);
    }

    [Fact]
    public async Task WarmUpAsync_WhenDisposedConcurrently_DoesNotThrow()
    {
        var warmUpTask = _pool.WarmUpAsync(1920, 1080, CancellationToken.None);
        _pool.Dispose();

        await warmUpTask;

        Assert.False(_pool.HasWarmDevice);
    }

    [Fact]
    public async Task WarmUpAsync_WhenCancellationIsRequested_DoesNotCreateADevice()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await _pool.WarmUpAsync(cancellationToken: cancellation.Token);

        Assert.Empty(_created);
        Assert.False(_pool.HasWarmDevice);
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
