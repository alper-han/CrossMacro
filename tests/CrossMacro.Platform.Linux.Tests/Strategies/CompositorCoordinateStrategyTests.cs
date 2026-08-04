namespace CrossMacro.Platform.Linux.Tests.Strategies;

public sealed class CompositorCoordinateStrategyTests
{
    [Fact]
    public async Task AbsoluteStrategy_ShouldPublishLogicalPositionsIncludingOrigin()
    {
        using var provider = new NotifyingPositionProvider((0, 0));
        using var strategy = new CompositorCoordinateStrategy(provider, emitRelativeCoordinates: false);
        var samples = new List<CoordinateSample>();
        strategy.SampleAvailable += (_, e) => samples.Add(e.Sample);
        await strategy.InitializeAsync(CancellationToken.None);

        provider.Publish(10, 5);
        provider.Publish(0, 0);

        _ = samples.Should().Equal(
            CoordinateSample.Create(10, 5),
            CoordinateSample.Create(0, 0));
    }

    [Fact]
    public async Task AbsoluteStrategy_WhenInitialQueryIsUnavailable_ShouldPublishFirstNotification()
    {
        using var provider = new NotifyingPositionProvider(initialPosition: null);
        using var strategy = new CompositorCoordinateStrategy(provider, emitRelativeCoordinates: false);
        var samples = new List<CoordinateSample>();
        strategy.SampleAvailable += (_, e) => samples.Add(e.Sample);
        await strategy.InitializeAsync(CancellationToken.None);

        provider.Publish(-25, 30);

        _ = samples.Should().ContainSingle().Which.Should().Be(CoordinateSample.Create(-25, 30));
    }

    [Fact]
    public async Task RelativeStrategy_ShouldPublishLogicalPixelDeltas()
    {
        using var provider = new NotifyingPositionProvider((100, 80));
        using var strategy = new CompositorCoordinateStrategy(provider, emitRelativeCoordinates: true);
        var samples = new List<CoordinateSample>();
        strategy.SampleAvailable += (_, e) => samples.Add(e.Sample);
        await strategy.InitializeAsync(CancellationToken.None);

        provider.Publish(110, 75);
        provider.Publish(115, 90);

        _ = samples.Should().Equal(
            CoordinateSample.Create(10, -5),
            CoordinateSample.Create(5, 15));
    }

    [Fact]
    public async Task RelativeStrategy_ShouldPreserveEveryRapidPositionNotificationInOrder()
    {
        using var provider = new NotifyingPositionProvider((0, 0));
        using var strategy = new CompositorCoordinateStrategy(provider, emitRelativeCoordinates: true);
        var samples = new List<CoordinateSample>();
        strategy.SampleAvailable += (_, e) => samples.Add(e.Sample);
        await strategy.InitializeAsync(CancellationToken.None);

        const int sampleCount = 2048;
        for (var index = 1; index <= sampleCount; index++)
        {
            provider.Publish(index, -index);
        }

        _ = samples.Should().HaveCount(sampleCount);
        _ = samples.Should().OnlyContain(sample => sample == CoordinateSample.Create(1, -1));
        _ = samples.Sum(sample => sample.X).Should().Be(sampleCount);
        _ = samples.Sum(sample => sample.Y).Should().Be(-sampleCount);
    }

    [Fact]
    public async Task RelativeStrategy_WhenProviderReconnects_ShouldResetDeltaBaseline()
    {
        using var provider = new NotifyingPositionProvider((100, 80));
        using var strategy = new CompositorCoordinateStrategy(provider, emitRelativeCoordinates: true);
        var samples = new List<CoordinateSample>();
        strategy.SampleAvailable += (_, e) => samples.Add(e.Sample);
        await strategy.InitializeAsync(CancellationToken.None);

        provider.Publish(110, 90);
        provider.Publish(500, 400, isDiscontinuity: true);
        provider.Publish(505, 395);

        _ = samples.Should().Equal(
            CoordinateSample.Create(10, 10),
            CoordinateSample.Create(5, -5));
    }

    [Fact]
    public async Task RelativeStrategy_WithoutNotifications_ShouldPollLogicalPixelDeltas()
    {
        using var provider = new PollingPositionProvider((100, 80), (110, 75));
        using var strategy = new CompositorCoordinateStrategy(provider, emitRelativeCoordinates: true);
        var sampleSource = new TaskCompletionSource<CoordinateSample>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        strategy.SampleAvailable += (_, e) => sampleSource.TrySetResult(e.Sample);
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(2),
            TimeProvider.System);

        await strategy.InitializeAsync(timeout.Token);
        _ = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.MouseMove });
        var sample = await sampleSource.Task.WaitAsync(timeout.Token);

        _ = sample.Should().Be(CoordinateSample.Create(10, -5));
    }

    [Fact]
    public async Task NotificationStrategy_WhenNotificationsStop_ShouldRecoverThroughActivityPolling()
    {
        using var provider = new RecoveringPositionProvider((100, 80), (110, 75));
        using var strategy = new CompositorCoordinateStrategy(provider, emitRelativeCoordinates: true);
        var sampleSource = new TaskCompletionSource<CoordinateSample>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        strategy.SampleAvailable += (_, e) => sampleSource.TrySetResult(e.Sample);
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(2),
            TimeProvider.System);

        await strategy.InitializeAsync(timeout.Token);
        provider.Publish(100, 80);
        await Task.Delay(TimeSpan.FromMilliseconds(20), TimeProvider.System, timeout.Token);
        _ = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.MouseMove });
        var sample = await sampleSource.Task.WaitAsync(timeout.Token);

        _ = sample.Should().Be(CoordinateSample.Create(10, -5));
    }

    [Fact]
    public async Task NotificationStrategy_WhenRecoveryQueryFinishesLate_ShouldIgnoreItsStalePosition()
    {
        using var provider = new RacingRecoveryPositionProvider((100, 80));
        using var strategy = new CompositorCoordinateStrategy(provider, emitRelativeCoordinates: true);
        var samples = new ConcurrentQueue<CoordinateSample>();
        strategy.SampleAvailable += (_, e) => samples.Enqueue(e.Sample);
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(2),
            TimeProvider.System);

        await strategy.InitializeAsync(timeout.Token);
        provider.Publish(100, 80);
        await Task.Delay(TimeSpan.FromMilliseconds(20), TimeProvider.System, timeout.Token);

        _ = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.MouseMove });
        await provider.RecoveryQueryStarted.Task.WaitAsync(timeout.Token);
        provider.Publish(120, 90);
        provider.CompleteRecoveryQuery((110, 85));
        await provider.RecoveryQueryReturned.Task.WaitAsync(timeout.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(20), TimeProvider.System, timeout.Token);

        _ = samples.Should().Equal(CoordinateSample.Create(20, 10));
    }

    [Fact]
    public async Task PollingStrategy_WhenIdle_ShouldNotContinuouslyQueryCompositor()
    {
        using var provider = new CountingPositionProvider();
        using var strategy = new CompositorCoordinateStrategy(provider, emitRelativeCoordinates: false);

        await strategy.InitializeAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(30), TimeProvider.System, CancellationToken.None);

        _ = provider.QueryCount.Should().Be(1);
    }

    [Fact]
    public async Task PollingStrategy_WhenOnlySyncReportsArrive_ShouldRemainIdle()
    {
        using var provider = new CountingPositionProvider();
        using var strategy = new CompositorCoordinateStrategy(provider, emitRelativeCoordinates: false);

        await strategy.InitializeAsync(CancellationToken.None);
        _ = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Sync });
        await Task.Delay(TimeSpan.FromMilliseconds(30), TimeProvider.System, CancellationToken.None);

        _ = provider.QueryCount.Should().Be(1);
    }

    [Fact]
    public async Task AbsoluteStrategy_ShouldReturnCachedPositionForButtonWithoutDuplicatingMotion()
    {
        using var provider = new NotifyingPositionProvider((0, 0));
        using var strategy = new CompositorCoordinateStrategy(provider, emitRelativeCoordinates: false);
        await strategy.InitializeAsync(CancellationToken.None);

        var buttonSample = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.MouseButton });
        var motionSample = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.MouseMove });

        _ = buttonSample.Should().Be(CoordinateSample.Create(0, 0));
        _ = motionSample.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_ShouldNotOverwriteNewerNotificationWithStaleQueryResult()
    {
        using var provider = new NotifyingPositionProvider((100, 80));
        provider.OnQuery = () => provider.Publish(200, 150);
        using var strategy = new CompositorCoordinateStrategy(provider, emitRelativeCoordinates: true);
        var samples = new List<CoordinateSample>();
        strategy.SampleAvailable += (_, e) => samples.Add(e.Sample);

        await strategy.InitializeAsync(CancellationToken.None);
        provider.Publish(210, 160);

        _ = samples.Should().ContainSingle().Which.Should().Be(CoordinateSample.Create(10, 10));
    }

    [Fact]
    public async Task Dispose_ShouldStopPositionNotifications()
    {
        using var provider = new NotifyingPositionProvider((10, 10));
        var strategy = new CompositorCoordinateStrategy(provider, emitRelativeCoordinates: false);
        var samples = new List<CoordinateSample>();
        strategy.SampleAvailable += (_, e) => samples.Add(e.Sample);
        await strategy.InitializeAsync(CancellationToken.None);

        strategy.Dispose();
        provider.Publish(20, 20);

        _ = samples.Should().BeEmpty();
    }

    private sealed class NotifyingPositionProvider((int X, int Y)? initialPosition) :
        IMousePositionProvider,
        IMousePositionChangeSource
    {
        public string ProviderName => "Test compositor";
        public bool IsSupported => true;
        public bool SupportsAbsolutePosition => true;
        public Action? OnQuery { get; set; }
        public event EventHandler<MousePositionChangedEventArgs>? PositionChanged;

        public Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            OnQuery?.Invoke();
            return Task.FromResult<(int X, int Y)?>(initialPosition);
        }

        public Task<(int Width, int Height)?> GetScreenResolutionAsync()
            => Task.FromResult<(int Width, int Height)?>(null);

        public void Publish(int x, int y, bool isDiscontinuity = false)
            => PositionChanged?.Invoke(this, new MousePositionChangedEventArgs(x, y, isDiscontinuity));

        public void Dispose()
        {
        }
    }

    private sealed class RecoveringPositionProvider(
        (int X, int Y) initialPosition,
        (int X, int Y) recoveredPosition) :
        IMousePositionProvider,
        IMousePositionChangeSource
    {
        private int _queryCount;

        public string ProviderName => "Recovering compositor";
        public bool IsSupported => true;
        public event EventHandler<MousePositionChangedEventArgs>? PositionChanged;

        public Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            var position = Interlocked.Increment(ref _queryCount) is 1
                ? initialPosition
                : recoveredPosition;
            return Task.FromResult<(int X, int Y)?>(position);
        }

        public Task<(int Width, int Height)?> GetScreenResolutionAsync() =>
            Task.FromResult<(int Width, int Height)?>(null);

        public void Publish(int x, int y) =>
            PositionChanged?.Invoke(this, new MousePositionChangedEventArgs(x, y));

        public void Dispose()
        {
            PositionChanged = null;
        }
    }

    private sealed class PollingPositionProvider(
        (int X, int Y) initialPosition,
        (int X, int Y) changedPosition) : IMousePositionProvider
    {
        private int _queryCount;

        public string ProviderName => "Test polling compositor";
        public bool IsSupported => true;
        public bool SupportsAbsolutePosition => true;

        public Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            var position = Interlocked.Increment(ref _queryCount) is 1
                ? initialPosition
                : changedPosition;
            return Task.FromResult<(int X, int Y)?>(position);
        }

        public Task<(int Width, int Height)?> GetScreenResolutionAsync()
            => Task.FromResult<(int Width, int Height)?>(null);

        public void Dispose()
        {
        }
    }

    private sealed class RacingRecoveryPositionProvider((int X, int Y) initialPosition) :
        IMousePositionProvider,
        IMousePositionChangeSource
    {
        private readonly TaskCompletionSource<(int X, int Y)?> _recoveryQuery = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _queryCount;

        public string ProviderName => "Racing recovery compositor";
        public bool IsSupported => true;
        public TaskCompletionSource<bool> RecoveryQueryStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> RecoveryQueryReturned { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public event EventHandler<MousePositionChangedEventArgs>? PositionChanged;

        public async Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            if (Interlocked.Increment(ref _queryCount) is 1)
            {
                return initialPosition;
            }

            _ = RecoveryQueryStarted.TrySetResult(true);
            var position = await _recoveryQuery.Task.ConfigureAwait(false);
            _ = RecoveryQueryReturned.TrySetResult(true);
            return position;
        }

        public Task<(int Width, int Height)?> GetScreenResolutionAsync() =>
            Task.FromResult<(int Width, int Height)?>(null);

        public void Publish(int x, int y) =>
            PositionChanged?.Invoke(this, new MousePositionChangedEventArgs(x, y));

        public void CompleteRecoveryQuery((int X, int Y) position) =>
            _ = _recoveryQuery.TrySetResult(position);

        public void Dispose()
        {
            PositionChanged = null;
        }
    }

    private sealed class CountingPositionProvider : IMousePositionProvider
    {
        public int QueryCount => Volatile.Read(ref _queryCount);
        private int _queryCount;

        public string ProviderName => "Counting compositor";
        public bool IsSupported => true;

        public Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            _ = Interlocked.Increment(ref _queryCount);
            return Task.FromResult<(int X, int Y)?>((0, 0));
        }

        public Task<(int Width, int Height)?> GetScreenResolutionAsync() =>
            Task.FromResult<(int Width, int Height)?>(null);

        public void Dispose()
        {
        }
    }
}
