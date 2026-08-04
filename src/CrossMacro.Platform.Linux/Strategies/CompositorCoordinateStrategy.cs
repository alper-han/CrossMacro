namespace CrossMacro.Platform.Linux.Strategies;

public sealed class CompositorCoordinateStrategy(
    IMousePositionProvider positionProvider,
    bool emitRelativeCoordinates) : ICoordinateStrategy, ICoordinateSampleSource
{
    private static readonly TimeSpan NotificationRecoveryPollInterval = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan QueryPollInterval = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan ActivityIdleGrace = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromMilliseconds(100);

    private readonly IMousePositionProvider _positionProvider = positionProvider;
    private readonly bool _usesPositionNotifications = positionProvider is IMousePositionChangeSource;
    private readonly TimeSpan _activePollInterval = positionProvider is IMousePositionChangeSource
        ? NotificationRecoveryPollInterval
        : QueryPollInterval;
    private readonly Lock _positionLock = new();
    private readonly Lock _publicationLock = new();
    private readonly SemaphoreSlim _pollActivity = new(0, 1);
    private CancellationTokenSource? _pollCancellation;
    private Task? _pollTask;
    private bool _hasPosition;
    private int _currentX;
    private int _currentY;
    private long _lastActivityTimestamp;
    private long _lastNotificationTimestamp;
    private long _notificationGeneration;
    private int _disposed;

    public bool ProducesLogicalCoordinates => true;

    public bool ProducesRelativeCoordinates { get; } = emitRelativeCoordinates;

    public event EventHandler<CoordinateSampleEventArgs>? SampleAvailable;

    public async Task InitializeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_positionProvider is IMousePositionChangeSource changeSource)
        {
            changeSource.PositionChanged += OnPositionChanged;
        }

        var position = await _positionProvider.GetAbsolutePositionAsync()
            .WaitAsync(ct)
            .ConfigureAwait(false);
        if (position is not null)
        {
            SetInitialPositionIfUnknown(position.Value.X, position.Value.Y);
        }
        else
        {
            Log.Warning(
                "[CompositorCoordinateStrategy] {ProviderName} did not provide an initial cursor position.",
                _positionProvider.ProviderName);
        }

        _pollCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _pollTask = PollPositionAsync(_pollCancellation.Token);
        if (_usesPositionNotifications)
        {
            Log.Debug(
                "[CompositorCoordinateStrategy] Using position-change notifications with activity-driven recovery for {ProviderName}",
                _positionProvider.ProviderName);
        }
        else
        {
            Log.Debug(
                "[CompositorCoordinateStrategy] Using activity-driven {IntervalMs} ms position polling for {ProviderName}",
                _activePollInterval.TotalMilliseconds,
                _positionProvider.ProviderName);
        }
    }

    public CoordinateSample ProcessPosition(CapturedInputEvent e)
    {
        if (e.Type is InputEventType.MouseMove)
        {
            SignalPollingActivity();
        }

        if (ProducesRelativeCoordinates || e.Type is InputEventType.Sync or InputEventType.MouseMove)
        {
            return CoordinateSample.None;
        }

        lock (_positionLock)
        {
            return _hasPosition
                ? CoordinateSample.Create(_currentX, _currentY)
                : CoordinateSample.None;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
        {
            return;
        }

        if (_positionProvider is IMousePositionChangeSource changeSource)
        {
            changeSource.PositionChanged -= OnPositionChanged;
        }

        var cancellation = Interlocked.Exchange(location1: ref _pollCancellation, value: null);
        var pollTask = Interlocked.Exchange(location1: ref _pollTask, value: null);
        cancellation?.Cancel();

        if (cancellation is null)
        {
            _pollActivity.Dispose();
        }
        else
        {
            if (pollTask is null || pollTask.IsCompleted)
            {
                cancellation.Dispose();
                _pollActivity.Dispose();
            }
            else
            {
                _ = pollTask.ContinueWith(
                    static (_, state) =>
                    {
                        var resources = ((CancellationTokenSource Cancellation, SemaphoreSlim Activity))state!;
                        resources.Cancellation.Dispose();
                        resources.Activity.Dispose();
                    },
                    (cancellation, _pollActivity),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        GC.SuppressFinalize(this);
    }

    private async Task PollPositionAsync(CancellationToken cancellationToken)
    {
        var consecutiveErrors = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _pollActivity.WaitAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    do
                    {
                        bool notificationIsFresh = _usesPositionNotifications
                            && Stopwatch.GetElapsedTime(Volatile.Read(ref _lastNotificationTimestamp)) < ActivityIdleGrace;
                        if (!notificationIsFresh)
                        {
                            long notificationGeneration = Volatile.Read(ref _notificationGeneration);
                            var position = await _positionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
                            if (position is not null)
                            {
                                PublishRecoveryPosition(
                                    position.Value.X,
                                    position.Value.Y,
                                    notificationGeneration);
                                consecutiveErrors = 0;
                            }
                        }

                        await Task.Delay(_activePollInterval, TimeProvider.System, cancellationToken).ConfigureAwait(false);
                    }
                    while (Stopwatch.GetElapsedTime(Volatile.Read(ref _lastActivityTimestamp)) < ActivityIdleGrace);

                    while (await _pollActivity.WaitAsync(0, cancellationToken).ConfigureAwait(false))
                    {
                        // Drain coalesced movement reports.
                    }

                    if (Stopwatch.GetElapsedTime(Volatile.Read(ref _lastActivityTimestamp)) < ActivityIdleGrace)
                    {
                        WakePollingWorker();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    consecutiveErrors++;
                    if (consecutiveErrors <= 3)
                    {
                        Log.Warning(
                            ex,
                            "[CompositorCoordinateStrategy] Position query failed for {ProviderName}",
                            _positionProvider.ProviderName);
                    }

                    await Task.Delay(ErrorBackoff, TimeProvider.System, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }

    private void SignalPollingActivity()
    {
        Volatile.Write(ref _lastActivityTimestamp, Stopwatch.GetTimestamp());
        WakePollingWorker();
    }

    private void WakePollingWorker()
    {
        if (Volatile.Read(ref _disposed) is not 0 || _pollActivity.CurrentCount is not 0)
        {
            return;
        }

        try
        {
            _ = _pollActivity.Release();
        }
        catch (SemaphoreFullException)
        {
            // Another input report signalled the worker concurrently.
        }
        catch (ObjectDisposedException)
        {
            // Disposal won the race with the final input report.
        }
    }

    private void OnPositionChanged(object? sender, MousePositionChangedEventArgs e)
    {
        lock (_publicationLock)
        {
            if (Volatile.Read(ref _disposed) is not 0)
            {
                return;
            }

            Volatile.Write(ref _lastNotificationTimestamp, Stopwatch.GetTimestamp());
            _ = Interlocked.Increment(ref _notificationGeneration);
            PublishPositionCore(e.X, e.Y, e.IsDiscontinuity);
        }
    }

    private void SetInitialPositionIfUnknown(int x, int y)
    {
        lock (_positionLock)
        {
            if (_hasPosition)
            {
                return;
            }

            _currentX = x;
            _currentY = y;
            _hasPosition = true;
        }
    }

    private void PublishRecoveryPosition(int x, int y, long notificationGeneration)
    {
        lock (_publicationLock)
        {
            if (Volatile.Read(ref _disposed) is not 0 ||
                (_usesPositionNotifications &&
                 Volatile.Read(ref _notificationGeneration) != notificationGeneration))
            {
                return;
            }

            PublishPositionCore(x, y, isDiscontinuity: false);
        }
    }

    private void PublishPositionCore(int x, int y, bool isDiscontinuity)
    {
        CoordinateSample sample;
        lock (_positionLock)
        {
            if (!_hasPosition || isDiscontinuity)
            {
                _currentX = x;
                _currentY = y;
                _hasPosition = true;
                if (ProducesRelativeCoordinates)
                {
                    return;
                }

                sample = CoordinateSample.Create(x, y);
            }
            else
            {
                if (_currentX == x && _currentY == y)
                {
                    return;
                }

                sample = ProducesRelativeCoordinates
                    ? CoordinateSample.Create(
                        (int)Math.Clamp((long)x - _currentX, int.MinValue, int.MaxValue),
                        (int)Math.Clamp((long)y - _currentY, int.MinValue, int.MaxValue))
                    : CoordinateSample.Create(x, y);
                _currentX = x;
                _currentY = y;
            }
        }

        SampleAvailable?.Invoke(this, new CoordinateSampleEventArgs(sample));
    }
}
