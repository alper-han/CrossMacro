namespace CrossMacro.Platform.Linux.Tests.DisplayServer;

public sealed class CompositeMousePositionProviderTests
{
    [Fact]
    public async Task InitializationTask_WhenPrimaryIsReady_DoesNotWaitForFallback()
    {
        var pendingFallbackInitialization = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var primary = new TestPositionProvider(
            isSupported: true,
            initializationTask: Task.FromResult(true));
        using var fallback = new TestPositionProvider(
            isSupported: true,
            initializationTask: pendingFallbackInitialization.Task);
        await using var provider = new CompositeMousePositionProvider(primary, fallback);

        bool initialized = await provider.InitializationTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(initialized);
    }

    [Fact]
    public async Task GetAbsolutePositionAsync_WhenPrimaryFails_UsesFallbackInsteadOfStalePrimaryPosition()
    {
        using var primary = new TestPositionProvider(
            isSupported: false,
            position: (10, 20));
        using var fallback = new TestPositionProvider(
            isSupported: true,
            position: (-30, 40));
        await using var provider = new CompositeMousePositionProvider(primary, fallback);

        var position = await provider.GetAbsolutePositionAsync();

        Assert.Equal((-30, 40), position);
        Assert.Equal(0, primary.PositionQueryCount);
        Assert.Equal(1, fallback.PositionQueryCount);
    }

    [Fact]
    public async Task PositionChanged_ForwardsPrimaryNotifications()
    {
        using var primary = new TestPositionProvider(isSupported: true);
        using var fallback = new TestPositionProvider(isSupported: true);
        await using var provider = new CompositeMousePositionProvider(primary, fallback);
        MousePositionChangedEventArgs? observed = null;
        provider.PositionChanged += (_, e) => observed = e;

        primary.Publish(-5, 7);

        Assert.NotNull(observed);
        Assert.Equal(-5, observed.X);
        Assert.Equal(7, observed.Y);
    }

    [Fact]
    public async Task GetAbsolutePositionAsync_WhenPrimaryNotificationIsFresh_UsesPrimary()
    {
        using var primary = new TestPositionProvider(
            isSupported: true,
            position: (10, 20));
        using var fallback = new TestPositionProvider(
            isSupported: true,
            position: (-30, 40));
        await using var provider = new CompositeMousePositionProvider(primary, fallback);

        primary.Publish(11, 21);
        var position = await provider.GetAbsolutePositionAsync();

        Assert.Equal((11, 21), position);
        Assert.Equal(1, primary.PositionQueryCount);
        Assert.Equal(0, fallback.PositionQueryCount);
    }

    [Fact]
    public async Task GetAbsolutePositionAsync_WhenPrimaryHasNoFreshNotification_UsesQueryableFallback()
    {
        using var primary = new TestPositionProvider(
            isSupported: true,
            position: (10, 20));
        using var fallback = new TestPositionProvider(
            isSupported: true,
            position: (-30, 40));
        await using var provider = new CompositeMousePositionProvider(primary, fallback);

        var position = await provider.GetAbsolutePositionAsync();

        Assert.Equal((-30, 40), position);
        Assert.Equal(1, primary.PositionQueryCount);
        Assert.Equal(1, fallback.PositionQueryCount);
    }

    private sealed class TestPositionProvider : IMousePositionProvider, IMousePositionChangeSource
    {
        private readonly Task<bool> _initializationTask;
        private (int X, int Y)? _position;
        private int _positionQueryCount;

        public TestPositionProvider(
            bool isSupported,
            (int X, int Y)? position = null,
            Task<bool>? initializationTask = null)
        {
            IsSupported = isSupported;
            _position = position;
            _initializationTask = initializationTask ?? Task.FromResult(true);
        }

        public string ProviderName => "Test position provider";
        public bool IsSupported { get; }
        public int PositionQueryCount => Volatile.Read(ref _positionQueryCount);
        public Task<bool> InitializationTask => _initializationTask;
        public event EventHandler<MousePositionChangedEventArgs>? PositionChanged;

        public Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            _ = Interlocked.Increment(ref _positionQueryCount);
            return Task.FromResult(_position);
        }

        public Task<(int Width, int Height)?> GetScreenResolutionAsync() =>
            Task.FromResult<(int Width, int Height)?>(null);

        public void Publish(int x, int y)
        {
            _position = (x, y);
            PositionChanged?.Invoke(this, new MousePositionChangedEventArgs(x, y));
        }

        public void Dispose()
        {
            PositionChanged = null;
        }
    }
}
