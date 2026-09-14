namespace CrossMacro.UI.Tests.ViewModels;

public sealed class EditorStateOwnershipTests
{
    [Fact]
    public void History_CoalescesCoordinateAliasesWithinWindowAndSeparatesLaterEdits()
    {
        var time = new ManualTimeProvider();
        var history = new EditorHistory(time);
        var action = new EditorAction();
        Assert.False(history.ShouldCoalesce(action, nameof(EditorAction.X)));
        time.Advance(TimeSpan.FromMilliseconds(399));
        Assert.True(history.ShouldCoalesce(action, nameof(EditorAction.CoordinateXToken)));
        time.Advance(TimeSpan.FromMilliseconds(401));
        Assert.False(history.ShouldCoalesce(action, nameof(EditorAction.X)));
        Assert.False(history.ShouldCoalesce(new EditorAction(), nameof(EditorAction.X)));
    }

    [Fact]
    public void History_WhenUtcClockMovesBack_StillSeparatesEditsAfterMonotonicWindow()
    {
        var time = new ManualTimeProvider();
        var history = new EditorHistory(time);
        var action = new EditorAction();
        var firstEditUtc = time.GetUtcNow();
        Assert.False(history.ShouldCoalesce(action, nameof(EditorAction.X)));

        time.Advance(TimeSpan.FromMilliseconds(401));
        time.RewindUtc(TimeSpan.FromHours(1));

        Assert.True(time.GetUtcNow() < firstEditUtc);
        Assert.False(history.ShouldCoalesce(action, nameof(EditorAction.CoordinateXToken)));
    }

    [Fact]
    public void DocumentSavedState_IsDetachedFromEditsAndFromAnotherDocument()
    {
        var first = new EditorDocumentSession();
        var second = new EditorDocumentSession();
        first.Actions.Add(new EditorAction { X = 4 });
        first.ImageAssets["image"] = "old";
        first.MarkClean(first.Capture("name", skipInitialZeroZero: false));
        second.MarkClean(second.Capture("other", skipInitialZeroZero: false));
        first.Actions[0].X = 8;
        Assert.True(first.IsDirty("name", skipInitialZeroZero: false));
        Assert.False(second.IsDirty("other", skipInitialZeroZero: false));
        first.Actions[0].X = 4;
        Assert.False(first.IsDirty("name", skipInitialZeroZero: false));
        first.ImageAssets["image"] = "new";
        Assert.True(first.IsDirty("name", skipInitialZeroZero: false));
    }

    [Fact]
    public void Selection_ReplacingFromOwnCollectionPreservesTheSnapshot()
    {
        var selection = new EditorSelection();
        selection.ReplaceIndices([3, 1]);
        selection.ReplaceIndices(selection.UnderlyingIndices);
        Assert.Equal([3, 1], selection.UnderlyingIndices);
        Assert.Equal([1, 3], EditorSelection.Normalize([3, -1, 1, 1, 4], actionCount: 4));
    }

    [Fact]
    public void ClosingIdleDocument_DoesNotCancelAnotherDocumentsCapture()
    {
        var service = Substitute.For<ICoordinateCaptureService>();
        using var active = new EditorCaptureSession(TimeProvider.System);
        var idle = new EditorCaptureSession(TimeProvider.System);
        using var lease = active.Begin();
        idle.Dispose();
        Assert.False(lease.Token.IsCancellationRequested);
        service.DidNotReceive().CancelCapture();
        active.Dispose();
        Assert.True(lease.Token.IsCancellationRequested);
        service.DidNotReceive().CancelCapture();
    }

    [Fact]
    public async Task ClosingDocument_StopsOnlyItsOwnedPlaybackAndSettlesCancellation()
    {
        var player = Substitute.For<IMacroPlayer>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, call.Arg<CancellationToken>());
            });
        var playback = new EditorTestPlayback(player);
        EditorTestPlaybackResult? result = null;
        var run = playback.PlayAsync(new MacroSequence(), () => Task.CompletedTask,
            value => { result = value; return Task.CompletedTask; });
        await started.Task;
        using var idle = new EditorTestPlayback(player);
        idle.Dispose();
        player.DidNotReceive().StopPlayback();
        playback.Dispose();
        await run;
        Assert.Equal(EditorTestPlaybackOutcome.Cancelled, result?.Outcome);
        Assert.False(playback.IsActive);
        player.Received(1).StopPlayback();
    }

    [Fact]
    public void CancelingEarlierDocumentCapture_DoesNotCancelNewerDocumentCapture()
    {
        using var first = new EditorCaptureSession(TimeProvider.System);
        using var second = new EditorCaptureSession(TimeProvider.System);
        using var firstLease = first.Begin();
        using var secondLease = second.Begin();
        first.Cancel();
        Assert.True(firstLease.Token.IsCancellationRequested);
        Assert.False(secondLease.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task ClosingDocumentBeforeUiStartCompletes_DoesNotStartPlayer()
    {
        var player = Substitute.For<IMacroPlayer>();
        var uiStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseUi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var playback = new EditorTestPlayback(player);
        var run = playback.PlayAsync(new MacroSequence(), () => { uiStarted.SetResult(); return releaseUi.Task; }, _ => Task.CompletedTask);
        await uiStarted.Task;
        playback.Dispose();
        releaseUi.SetResult();
        await run;
        await player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
        player.DidNotReceive().StopPlayback();
    }

    [Fact]
    public async Task ConcurrentStop_WaitsForTheSameCancellationHandshake()
    {
        var player = Substitute.For<IMacroPlayer>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseCancellation = new ManualResetEventSlim();
        _ = player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var token = call.Arg<CancellationToken>();
                using var registration = token.Register(() =>
                {
                    cancellationEntered.SetResult();
                    releaseCancellation.Wait(CancellationToken.None);
                });
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, token);
            });
        using var playback = new EditorTestPlayback(player);
        var run = playback.PlayAsync(new MacroSequence(), () => Task.CompletedTask, _ => Task.CompletedTask);
        await started.Task;
        var firstStop = playback.StopAsync();
        await cancellationEntered.Task;
        var secondStop = playback.StopAsync();
        try { Assert.False(secondStop.IsCompleted); }
        finally { releaseCancellation.Set(); }
        await Task.WhenAll(firstStop, secondStop, run);
        player.Received(1).StopPlayback();
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
        private long _timestamp;
        public override DateTimeOffset GetUtcNow() => _now;
        public override long GetTimestamp() => _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        internal void Advance(TimeSpan amount)
        {
            _now += amount;
            _timestamp += amount.Ticks;
        }
        internal void RewindUtc(TimeSpan amount) => _now -= amount;
    }
}
