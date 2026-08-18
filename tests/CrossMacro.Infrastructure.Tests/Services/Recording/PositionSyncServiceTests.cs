
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
        await queryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await Task.Run(_service.StopPositionSync).WaitAsync(TimeSpan.FromSeconds(2));

        _ = _service.IsRunning.Should().BeFalse();
    }
}
