namespace CrossMacro.Infrastructure.Tests.Services.Playback;

public sealed class PlaybackSessionResourceOwnerTests
{
    private static readonly ushort[] SavedButtons = [272, 273];

    [Fact]
    public async Task AcquireAsync_WithPooledSimulator_UsesReadyLeaseWithoutReinitializing()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var pool = Substitute.For<IInputSimulatorPool>();
        _ = pool.AcquireAsync(1920, 1080, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IInputSimulator>(simulator));
        var owner = new PlaybackSessionResourceOwner(simulatorFactory: null, simulatorPool: pool);

        owner.Begin(TestContext.Current.CancellationToken);
        await owner.AcquireAsync(1920, 1080, TestContext.Current.CancellationToken);
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

        owner.Begin(TestContext.Current.CancellationToken);
        await owner.AcquireAsync(1920, 1080, TestContext.Current.CancellationToken);

        _ = owner.Simulator.Should().BeSameAs(simulator);
        await simulator.Received(1).InitializeAsync(1920, 1080, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RuntimeVariables_DoesNotExposeMutableSessionDictionary()
    {
        using var owner = new PlaybackSessionResourceOwner(simulatorFactory: null, simulatorPool: null);
        owner.Begin(TestContext.Current.CancellationToken);
        owner.Variables["Result"] = "ok";

        var variables = owner.RuntimeVariables;

        _ = variables.Should().ContainKey("result").WhoseValue.Should().Be("ok");
        var mutableView = Assert.IsAssignableFrom<IDictionary<string, string>>(variables);
        _ = Assert.Throws<NotSupportedException>(() => mutableView["result"] = "changed");
        _ = variables["result"].Should().Be("ok");
        owner.End();
    }

    [Fact]
    public void Begin_WhenAlreadyPlaying_Throws()
    {
        using var owner = new PlaybackSessionResourceOwner(simulatorFactory: null, simulatorPool: null);
        owner.Begin(TestContext.Current.CancellationToken);

        var act = () => owner.Begin(TestContext.Current.CancellationToken);

        _ = act.Should().Throw<PlaybackAlreadyInProgressException>();
    }

    [Fact]
    public async Task TryPause_ThenResume_BlocksWaiterUntilResumed()
    {
        using var owner = new PlaybackSessionResourceOwner(simulatorFactory: null, simulatorPool: null);
        owner.Begin(TestContext.Current.CancellationToken);

        _ = owner.TryPause(out _, out _).Should().BeTrue();
        var waiter = owner.WaitIfPausedAsync(TestContext.Current.CancellationToken);

        _ = owner.IsPaused.Should().BeTrue();
        _ = waiter.IsCompleted.Should().BeFalse();

        _ = owner.TryResume().Should().BeTrue();
        await waiter.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, TestContext.Current.CancellationToken);

        _ = owner.IsPaused.Should().BeFalse();
        _ = owner.PauseResumeVersion.Should().Be(1);
    }

    [Fact]
    public void TryPause_WhenNotPlayingOrAlreadyPaused_ReturnsFalse()
    {
        using var owner = new PlaybackSessionResourceOwner(simulatorFactory: null, simulatorPool: null);

        _ = owner.TryPause(out _, out _).Should().BeFalse();

        owner.Begin(TestContext.Current.CancellationToken);
        _ = owner.TryPause(out _, out _).Should().BeTrue();
        _ = owner.TryPause(out _, out _).Should().BeFalse();
    }

    [Fact]
    public async Task TryPause_ReportsSavedInputAndResumeRestoresIt()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var buttons = Substitute.For<IButtonStateTracker>();
        var keys = Substitute.For<IKeyStateTracker>();
        _ = buttons.PressedButtons.Returns(SavedButtons);
        _ = keys.PressedKeys.Returns([30]);
        using var owner = new PlaybackSessionResourceOwner(() => simulator, simulatorPool: null);
        owner.Begin(TestContext.Current.CancellationToken);
        await owner.AcquireAsync(1920, 1080, TestContext.Current.CancellationToken);
        owner.AttachInputState(executor: null, buttons, keys);

        _ = owner.TryPause(out var savedButtons, out var savedKeys).Should().BeTrue();
        _ = owner.TryResume().Should().BeTrue();

        _ = savedButtons.Should().Be(2);
        _ = savedKeys.Should().Be(1);
        buttons.Received(1).RestoreAll(simulator, Arg.Is<IEnumerable<ushort>>(set => set.SequenceEqual(SavedButtons)));
    }

    [Fact]
    public async Task StopPlayback_WhilePaused_ReleasesWaiterAndCancelsToken()
    {
        using var owner = new PlaybackSessionResourceOwner(simulatorFactory: null, simulatorPool: null);
        owner.Begin(TestContext.Current.CancellationToken);
        var token = owner.Token;
        _ = owner.TryPause(out _, out _).Should().BeTrue();
        var waiter = owner.WaitIfPausedAsync(TestContext.Current.CancellationToken);

        owner.StopPlayback();

        await waiter.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, TestContext.Current.CancellationToken);
        _ = token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task End_WhilePaused_ReleasesWaiterAndMarksNotPlaying()
    {
        using var owner = new PlaybackSessionResourceOwner(simulatorFactory: null, simulatorPool: null);
        owner.Begin(TestContext.Current.CancellationToken);
        _ = owner.TryPause(out _, out _).Should().BeTrue();
        var waiter = owner.WaitIfPausedAsync(TestContext.Current.CancellationToken);

        owner.End();

        await waiter.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, TestContext.Current.CancellationToken);
        _ = owner.IsPlaying.Should().BeFalse();
        _ = owner.IsPaused.Should().BeFalse();
    }

    [Fact]
    public async Task TryPause_WhenCalledConcurrently_SucceedsExactlyOnce()
    {
        using var owner = new PlaybackSessionResourceOwner(simulatorFactory: null, simulatorPool: null);
        owner.Begin(TestContext.Current.CancellationToken);
        const int Contenders = 16;
        using var barrier = new Barrier(Contenders);
        var successes = 0;

        var tasks = Enumerable.Range(0, Contenders).Select(_ => Task.Factory.StartNew(
            () =>
            {
                barrier.SignalAndWait();
                if (owner.TryPause(out _, out _))
                {
                    _ = Interlocked.Increment(ref successes);
                }
            },
            TestContext.Current.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)).ToArray();
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30), TimeProvider.System, TestContext.Current.CancellationToken);

        _ = successes.Should().Be(1);
        _ = owner.IsPaused.Should().BeTrue();
    }

    [Fact]
    public async Task PauseResumeAndStop_RaceFromManyThreads_NeverStrandsAWaiter()
    {
        using var owner = new PlaybackSessionResourceOwner(simulatorFactory: null, simulatorPool: null);
        owner.Begin(TestContext.Current.CancellationToken);
        using var barrier = new Barrier(3);

        var pauser = Task.Factory.StartNew(
            () =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < 2_000; i++)
                {
                    _ = owner.TryPause(out _, out _);
                    _ = owner.TryResume();
                }
            },
            TestContext.Current.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var resumer = Task.Factory.StartNew(
            () =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < 2_000; i++)
                {
                    _ = owner.TryResume();
                    _ = owner.TryPause(out _, out _);
                }
            },
            TestContext.Current.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var stopper = Task.Factory.StartNew(
            () =>
            {
                barrier.SignalAndWait();
                owner.StopPlayback();
            },
            TestContext.Current.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await Task.WhenAll(pauser, resumer, stopper).WaitAsync(TimeSpan.FromSeconds(30), TimeProvider.System, TestContext.Current.CancellationToken);

        owner.StopPlayback();
        var waiter = owner.WaitIfPausedAsync(TestContext.Current.CancellationToken);

        _ = waiter.IsCompleted.Should().BeTrue();
    }
}
