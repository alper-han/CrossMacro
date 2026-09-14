
namespace CrossMacro.Infrastructure.Tests.Services.Recording;

public sealed class PositionSyncServiceTests : IDisposable
{
    private readonly IMousePositionProvider _providerSubstitute;
    private readonly PositionSyncService _service;
    private readonly CancellationTokenSource _cts;

    public PositionSyncServiceTests()
    {
        _providerSubstitute = Substitute.For<IMousePositionProvider>();
        _service = new PositionSyncService(_providerSubstitute);
        _cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        _service.StopPositionSync();
        _cts.Dispose();
    }

    [Fact]
    public void IsRunning_ShouldBeFalse_Initially()
    {
        _ = _service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_ShouldNotStart_IfProviderNotSupported()
    {
        // Arrange
        _ = _providerSubstitute.IsSupported.Returns(returnThis: false);
        var callback = Substitute.For<Action<int, int, long>>();

        // Act
        await _service.StartAsync(callback, () => (0, 0), _cts.Token);

        // Assert
        _ = _service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_ShouldStart_IfProviderSupported()
    {
        // Arrange
        _ = _providerSubstitute.IsSupported.Returns(returnThis: true);
        var callback = Substitute.For<Action<int, int, long>>();

        // Act
        await _service.StartAsync(callback, () => (0, 0), _cts.Token);

        // Assert
        _ = _service.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_UsesInjectedTimeProviderWithExplicitDelayReadiness()
    {
        var timeProvider = new FakeTimeProvider();
        var delayRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var positionChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = Substitute.For<IMousePositionProvider>();
        _ = provider.IsSupported.Returns(returnThis: true);
        _ = provider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>(new(10, 10)));
        using var service = new PositionSyncService(
            provider,
            timeProvider,
            (delay, cancellationToken) =>
            {
                var pendingDelay = Task.Delay(delay, timeProvider, cancellationToken);
                _ = delayRegistered.TrySetResult();
                return pendingDelay;
            });

        var cancellationToken = TestContext.Current.CancellationToken;
        await service.StartAsync((_, _, _) => positionChanged.TrySetResult(), () => (0, 0), cancellationToken);
        await delayRegistered.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, cancellationToken);
        timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        await positionChanged.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, cancellationToken);

        _ = await provider.Received(1).GetAbsolutePositionAsync();
        service.StopPositionSync();
    }

    [Fact]
    public async Task Stop_WhenProviderQueryDoesNotObserveCancellation_ReturnsWithoutBlockingIndefinitely()
    {
        _ = _providerSubstitute.IsSupported.Returns(returnThis: true);

        var queryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _providerSubstitute.GetAbsolutePositionAsync()
            .Returns(unusedCallInfo =>
            {
                _ = queryStarted.TrySetResult();
                return Task.Delay(Timeout.Infinite, CancellationToken.None)
                    .ContinueWith<(int X, int Y)?>(
                        _ => null,
                        CancellationToken.None,
                        TaskContinuationOptions.None,
                        TaskScheduler.Default);
            });

        await _service.StartAsync((_, _, _) => { }, () => (0, 0), _cts.Token);
        await queryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, CancellationToken.None);

        await Task.Run(_service.StopPositionSync, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, CancellationToken.None);

        _ = _service.IsRunning.Should().BeFalse();
    }
}
