namespace CrossMacro.Infrastructure.Tests.Services.Playback;

public sealed class PlaybackSessionResourceOwnerTests
{
    [Fact]
    public async Task AcquireAsync_WithPooledSimulator_UsesReadyLeaseWithoutReinitializing()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var pool = Substitute.For<IInputSimulatorPool>();
        _ = pool.AcquireAsync(1920, 1080, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IInputSimulator>(simulator));
        var owner = new PlaybackSessionResourceOwner(simulatorFactory: null, simulatorPool: pool);

        owner.Begin(CancellationToken.None);
        await owner.AcquireAsync(1920, 1080, CancellationToken.None);
        owner.End();

        _ = await pool.Received(1).AcquireAsync(1920, 1080, Arg.Any<CancellationToken>());
        await simulator.DidNotReceive().InitializeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        pool.Received(1).Release(simulator, 1920, 1080);
    }

    [Fact]
    public async Task AcquireAsync_WithFactory_InitializesBeforeMakingSimulatorAvailable()
    {
        var simulator = Substitute.For<IInputSimulator>();
        using var owner = new PlaybackSessionResourceOwner(() => simulator, simulatorPool: null);

        owner.Begin(CancellationToken.None);
        await owner.AcquireAsync(1920, 1080, CancellationToken.None);

        _ = owner.Simulator.Should().BeSameAs(simulator);
        await simulator.Received(1).InitializeAsync(1920, 1080, Arg.Any<CancellationToken>());
    }
}
