
namespace CrossMacro.Platform.MacOS.Tests.Strategies;

public sealed class MacOSAbsoluteCoordinateStrategyTests
{
    [Fact]
    public async Task InitializeAsync_UsesInjectedPositionProvider()
    {
        using var provider = new StubMousePositionProvider((123, 456));
        using var strategy = new MacOSAbsoluteCoordinateStrategy(provider);

        await strategy.InitializeAsync(CancellationToken.None);

        var sample = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.Key,
            Code = InputEventCode.KEY_A,
            Value = 1,
        });

        Assert.Equal(CoordinateSample.Create(123, 456), sample);
        Assert.Equal(1, provider.AbsolutePositionQueries);
    }

    [Fact]
    public async Task InitializeAsync_WhenPositionQueryIsCanceled_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        using var provider = new CancelingMousePositionProvider(cancellation);
        using var strategy = new MacOSAbsoluteCoordinateStrategy(provider);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => strategy.InitializeAsync(cancellation.Token));
    }

    [Fact]
    public async Task InitializeAsync_WhenCanceledWhilePositionQueryIsPending_StopsWaiting()
    {
        using var cancellation = new CancellationTokenSource();
        using var provider = new DelayedMousePositionProvider((1, 2));
        using var strategy = new MacOSAbsoluteCoordinateStrategy(provider);

        var initialize = strategy.InitializeAsync(cancellation.Token);
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialize);
    }

    [Fact]
    public void ProcessPosition_WhenSyncEvent_ReturnsNoSample()
    {
        var strategy = new MacOSAbsoluteCoordinateStrategy(new StubMousePositionProvider(position: null));

        var result = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.Sync,
        });

        Assert.False(result.HasValue);
    }

    [Fact]
    public void ProcessPosition_WhenNonMouseMoveEvent_ReturnsLastKnownPosition()
    {
        var strategy = new MacOSAbsoluteCoordinateStrategy(new StubMousePositionProvider(position: null));

        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = 42,
        });
        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = 99,
        });

        var result = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.Key,
            Code = InputEventCode.KEY_A,
            Value = 1,
        });

        Assert.Equal(CoordinateSample.Create(42, 99), result);
    }

    [Fact]
    public void ProcessPosition_WhenAxesArriveSeparately_EmitsOneAtomicSampleOnSync()
    {
        var strategy = new MacOSAbsoluteCoordinateStrategy(new StubMousePositionProvider(position: null));

        var yResult = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = 15,
        });

        var xResult = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = 320,
        });
        var result = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Sync });

        Assert.False(yResult.HasValue);
        Assert.False(xResult.HasValue);
        Assert.Equal(CoordinateSample.Create(320, 15), result);
    }

    [Fact]
    public void ProcessPosition_WhenOnlyOneAxisChanges_EmitsUpdatedPositionOnSync()
    {
        var strategy = new MacOSAbsoluteCoordinateStrategy(new StubMousePositionProvider(position: null));

        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = 640,
        });

        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = 480,
        });
        var result = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Sync });

        Assert.Equal(CoordinateSample.Create(640, 480), result);
    }

    private sealed class CancelingMousePositionProvider(CancellationTokenSource cancellation) : IMousePositionProvider
    {
        public string ProviderName => "test";
        public bool IsSupported => true;

        public async Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            await cancellation.CancelAsync();
            return await Task.FromCanceled<(int X, int Y)?>(cancellation.Token);
        }

        public Task<(int Width, int Height)?> GetScreenResolutionAsync() => Task.FromResult<(int Width, int Height)?>(null);

        public void Dispose() { /* Test provider has no resources. */ }
    }
}
