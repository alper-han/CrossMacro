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

    [Fact]
    public async Task GetOutputBoundsAsync_WhenPrimaryHasTopology_UsesPrimary()
    {
        IReadOnlyList<ScreenRect> expected = [new ScreenRect(0, 0, 2560, 1440)];
        using var primary = new TestPositionProvider(isSupported: true, outputs: expected);
        using var fallback = new TestPositionProvider(
            isSupported: true,
            outputs: [new ScreenRect(2560, 0, 2560, 1440)]);
        await using var provider = new CompositeMousePositionProvider(primary, fallback);

        var outputs = await provider.GetOutputBoundsAsync(CancellationToken.None);

        Assert.Same(expected, outputs);
        Assert.Equal(1, primary.OutputQueryCount);
        Assert.Equal(0, fallback.OutputQueryCount);
    }

    [Fact]
    public async Task GetOutputBoundsAsync_WhenPrimaryTopologyIsEmpty_UsesFallback()
    {
        IReadOnlyList<ScreenRect> expected = [new ScreenRect(-1920, 0, 1920, 1080)];
        using var primary = new TestPositionProvider(isSupported: true);
        using var fallback = new TestPositionProvider(isSupported: false, outputs: expected);
        await using var provider = new CompositeMousePositionProvider(primary, fallback);

        var outputs = await provider.GetOutputBoundsAsync(CancellationToken.None);

        Assert.Same(expected, outputs);
        Assert.Equal(1, primary.OutputQueryCount);
        Assert.Equal(1, fallback.OutputQueryCount);
    }

    [Fact]
    public async Task Availability_SeparatesStaticCapabilityFromCurrentPosition()
    {
        using var primary = new AvailabilityPositionProvider(
            isSupported: true,
            isPositionAvailable: false);
        using var fallback = new AvailabilityPositionProvider(
            isSupported: false,
            isPositionAvailable: false);
        await using var provider = new CompositeMousePositionProvider(primary, fallback);

        Assert.True(provider.SupportsAbsolutePosition);
        Assert.False(provider.IsPositionAvailable);
        Assert.False(provider.HasUsableAbsolutePosition());

        primary.IsPositionAvailable = true;
        primary.Publish(10, 20);

        Assert.True(provider.IsPositionAvailable);
        Assert.True(provider.HasUsableAbsolutePosition());
    }

    private sealed class TestPositionProvider(
        bool isSupported,
        (int X, int Y)? position = null,
        Task<bool>? initializationTask = null,
        IReadOnlyList<ScreenRect>? outputs = null) :
        IMousePositionProvider,
        IMousePositionChangeSource,
        IOutputTopologyProvider
    {
        private readonly Task<bool> _initializationTask = initializationTask ?? Task.FromResult(true);
        private readonly IReadOnlyList<ScreenRect> _outputs = outputs ?? [];
        private int _positionQueryCount;
        private int _outputQueryCount;

        public string ProviderName => "Test position provider";
        public bool IsSupported { get; } = isSupported;
        public int PositionQueryCount => Volatile.Read(ref _positionQueryCount);
        public int OutputQueryCount => Volatile.Read(ref _outputQueryCount);
        public Task<bool> InitializationTask => _initializationTask;
        private (int X, int Y)? Position { get; set; } = position;
        public event EventHandler<MousePositionChangedEventArgs>? PositionChanged;

        public Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            _ = Interlocked.Increment(ref _positionQueryCount);
            return Task.FromResult(Position);
        }

        public Task<(int Width, int Height)?> GetScreenResolutionAsync() =>
            Task.FromResult<(int Width, int Height)?>(null);

        public Task<IReadOnlyList<ScreenRect>> GetOutputBoundsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = Interlocked.Increment(ref _outputQueryCount);
            return Task.FromResult(_outputs);
        }

        public void Publish(int x, int y)
        {
            Position = (x, y);
            PositionChanged?.Invoke(this, new MousePositionChangedEventArgs(x, y));
        }

        public void Dispose()
        {
            PositionChanged = null;
        }
    }

    private sealed class AvailabilityPositionProvider(
        bool isSupported,
        bool isPositionAvailable) :
        IMousePositionProvider,
        IMousePositionAvailability,
        IMousePositionChangeSource
    {
        public string ProviderName => "Availability position provider";
        public bool IsSupported { get; } = isSupported;
        public bool IsPositionAvailable { get; set; } = isPositionAvailable;

        public event EventHandler<MousePositionChangedEventArgs>? PositionChanged;

        public Task<(int X, int Y)?> GetAbsolutePositionAsync() =>
            Task.FromResult<(int X, int Y)?>(null);

        public Task<(int Width, int Height)?> GetScreenResolutionAsync() =>
            Task.FromResult<(int Width, int Height)?>(null);

        public void Publish(int x, int y) =>
            PositionChanged?.Invoke(this, new MousePositionChangedEventArgs(x, y));

        public void Dispose()
        {
            PositionChanged = null;
        }
    }
}
