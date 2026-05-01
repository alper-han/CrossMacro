using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services.Recording;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.Core.Tests.Services.Recording;

public class PositionSyncServiceTests : IDisposable
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
        _service.Stop();
        _cts.Dispose();
    }

    [Fact]
    public void IsRunning_ShouldBeFalse_Initially()
    {
        _service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_ShouldNotStart_IfProviderNotSupported()
    {
        // Arrange
        _providerSubstitute.IsSupported.Returns(false);
        var callback = Substitute.For<Action<int, int, long>>();

        // Act
        await _service.StartAsync(callback, () => (0, 0), _cts.Token);

        // Assert
        _service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_ShouldStart_IfProviderSupported()
    {
        // Arrange
        _providerSubstitute.IsSupported.Returns(true);
        var callback = Substitute.For<Action<int, int, long>>();

        // Act
        await _service.StartAsync(callback, () => (0, 0), _cts.Token);

        // Assert
        _service.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task Stop_WhenProviderQueryDoesNotObserveCancellation_ReturnsWithoutBlockingIndefinitely()
    {
        _providerSubstitute.IsSupported.Returns(true);

        var queryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _providerSubstitute.GetAbsolutePositionAsync()
            .Returns(_ =>
            {
                queryStarted.TrySetResult();
                return Task.Delay(Timeout.Infinite, CancellationToken.None)
                    .ContinueWith<(int X, int Y)?>(_ => null, CancellationToken.None);
            });

        await _service.StartAsync((_, _, _) => { }, () => (0, 0), _cts.Token);
        await queryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var started = DateTime.UtcNow;
        _service.Stop();
        var elapsed = DateTime.UtcNow - started;

        _service.IsRunning.Should().BeFalse();
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }
}
