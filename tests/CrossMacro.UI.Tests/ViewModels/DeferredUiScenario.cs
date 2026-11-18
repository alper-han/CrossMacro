namespace CrossMacro.UI.Tests.ViewModels;

/// <summary>
/// Choreographs a playback whose player completes on a background thread while the UI work it triggers is
/// queued on a <see cref="DeferredUiExecutor"/>, so tests can observe state before and after the queued work runs.
/// </summary>
internal sealed class DeferredUiScenario
{
    private readonly TaskCompletionSource<bool> _playStarted = NewSignal();
    private readonly TaskCompletionSource<bool> _playbackCompleted = NewSignal();
    private readonly TaskCompletionSource<bool> _cleanupStarted = NewSignal();
    private readonly TaskCompletionSource<bool> _allowPlayerReturn = NewSignal();
    private readonly SwitchingUiDispatcher _dispatcher;

    public DeferredUiScenario(IMacroPlayer player, SwitchingUiDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _ = player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(async unusedCallInfo =>
            {
                _playStarted.SetResult(true);
                _ = await _playbackCompleted.Task.ConfigureAwait(false);
                _cleanupStarted.SetResult(true);
                _ = await _allowPlayerReturn.Task.ConfigureAwait(false);
            });
    }

    public DeferredUiExecutor Executor { get; } = new();

    /// <summary>
    /// Routes UI work to the deferred executor, starts playback under its context and waits until the player is running.
    /// Set up view model state before calling this: anything dispatched earlier would otherwise be queued and never run.
    /// </summary>
    public async Task<Task> StartAsync(Func<Task> startPlayback)
    {
        _dispatcher.Target = Executor;
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(Executor);
        Task playTask;
        try
        {
            playTask = startPlayback();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        _ = await _playStarted.Task.WaitAsync(TestTimeouts.Default, TimeProvider.System, TestContext.Current.CancellationToken);
        return playTask;
    }

    /// <summary>Completes playback from a thread-pool thread and waits until the player has begun its cleanup.</summary>
    public async Task CompleteOffThreadAsync()
    {
        _ = await Task.Run(() => _playbackCompleted.TrySetResult(true), CancellationToken.None);
        _ = await _cleanupStarted.Task.WaitAsync(TestTimeouts.Default, TimeProvider.System, TestContext.Current.CancellationToken);
    }

    /// <summary>Lets the player return and waits until its completion posted UI work, which has not run yet.</summary>
    public async Task ReleasePlayerAsync(Task playTask)
    {
        _ = _allowPlayerReturn.TrySetResult(true);
        var firstCompleted = await Task.WhenAny(playTask, Executor.PostObserved.Task);

        Assert.Same(Executor.PostObserved.Task, firstCompleted);
    }

    /// <summary>Runs the queued UI work, including work queued by that work, and waits for playback to finish.</summary>
    public async Task DrainUiAsync(Task playTask)
    {
        var firstPostCount = Executor.PostCount;
        Executor.RunAll();
        await Executor.WaitForPostAfterAsync(firstPostCount, TestContext.Current.CancellationToken)
            .WaitAsync(TestTimeouts.Default, TimeProvider.System, TestContext.Current.CancellationToken);
        Executor.RunAll();
        await playTask.WaitAsync(TestTimeouts.Default, TimeProvider.System, TestContext.Current.CancellationToken);
    }

    private static TaskCompletionSource<bool> NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
