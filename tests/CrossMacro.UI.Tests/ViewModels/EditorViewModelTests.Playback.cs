namespace CrossMacro.UI.Tests.ViewModels;

public sealed partial class EditorViewModelTests
{
    [Fact]
    public async Task ToggleTestPlaybackAsync_WhenPlaybackCompletes_AllowsImmediateSecondRun()
    {
        // Arrange
        _viewModel.Actions.Add(new EditorAction
        {
            Type = EditorActionType.MouseClick,
            X = 10,
            Y = 20,
        });
        _ = _converter
            .ToMacroSequence(Arg.Any<EditorMacroProjection>())
            .Returns(new MacroSequence
            {
                Events = { new MacroEvent { Type = EventType.Click, X = 10, Y = 20 } },
            });
        _ = _macroPlayer
            .PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _viewModel.ToggleTestPlaybackAsync();
        await _viewModel.ToggleTestPlaybackAsync();

        // Assert
        _ = _viewModel.IsRunningTest.Should().BeFalse();
        await _macroPlayer.Received(2).PlayAsync(
            Arg.Any<MacroSequence>(),
            Arg.Any<PlaybackOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleTestPlaybackAsync_WhenRunning_StopsAndReportsCancellation()
    {
        // Arrange
        _viewModel.Actions.Add(new EditorAction
        {
            Type = EditorActionType.MouseClick,
            X = 10,
            Y = 20,
        });
        _ = _converter
            .ToMacroSequence(Arg.Any<EditorMacroProjection>())
            .Returns(new MacroSequence
            {
                Events = { new MacroEvent { Type = EventType.Click, X = 10, Y = 20 } },
            });

        var playbackStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _macroPlayer
            .PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(playbackStarted, call.Arg<CancellationToken>()));

        // Act
        var playbackTask = _viewModel.ToggleTestPlaybackAsync();
        _ = await playbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, CancellationToken.None);
        await _viewModel.ToggleTestPlaybackAsync();
        await playbackTask;

        // Assert
        _ = _viewModel.Status.Should().Be("Editor_StatusTestCancelled");
        _macroPlayer.Received(1).StopPlayback();
    }

    [Fact]
    public async Task ToggleTestPlaybackAsync_WhenStopIsRequested_WaitsForOldPlaybackBeforeNextRun()
    {
        // Arrange
        _viewModel.Actions.Add(new EditorAction
        {
            Type = EditorActionType.MouseClick,
            X = 10,
            Y = 20,
        });
        _ = _converter
            .ToMacroSequence(Arg.Any<EditorMacroProjection>())
            .Returns(new MacroSequence
            {
                Events = { new MacroEvent { Type = EventType.Click, X = 10, Y = 20 } },
            });

        var firstPlaybackStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstCancellationObserved = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstPlaybackRelease = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPlaybackStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPlaybackRelease = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var playInvocation = 0;
        _ = _macroPlayer
            .PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var invocation = Interlocked.Increment(ref playInvocation);
                return invocation is 1
                    ? WaitForCancellationThenReleaseAsync(firstPlaybackStarted, firstCancellationObserved, firstPlaybackRelease, call.Arg<CancellationToken>())
                    : SignalAndWaitAsync(secondPlaybackStarted, secondPlaybackRelease);
            });

        // Act
        var firstPlaybackTask = _viewModel.ToggleTestPlaybackAsync();
        _ = await firstPlaybackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, CancellationToken.None);
        await _viewModel.ToggleTestPlaybackAsync();
        _ = await firstCancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, CancellationToken.None);

        var secondPlaybackTask = _viewModel.ToggleTestPlaybackAsync();
        _ = secondPlaybackTask.IsCompleted.Should().BeTrue();
        _ = firstPlaybackRelease.TrySetResult(null);
        await firstPlaybackTask;

        secondPlaybackTask = _viewModel.ToggleTestPlaybackAsync();
        _ = await secondPlaybackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, CancellationToken.None);

        // Assert
        _macroPlayer.Received(1).StopPlayback();
        await _macroPlayer.Received(2).PlayAsync(
            Arg.Any<MacroSequence>(),
            Arg.Any<PlaybackOptions>(),
            Arg.Any<CancellationToken>());

        _ = secondPlaybackRelease.TrySetResult(null);
        await secondPlaybackTask;
    }

    private static async Task WaitForCancellationThenReleaseAsync(
        TaskCompletionSource<object?> playbackStarted,
        TaskCompletionSource<object?> cancellationObserved,
        TaskCompletionSource<object?> release,
        CancellationToken cancellationToken)
    {
        _ = playbackStarted.TrySetResult(null);
        using var registration = cancellationToken.Register(() => cancellationObserved.TrySetResult(null));
        _ = await release.Task;
    }

    private static async Task WaitForCancellationAsync(
        TaskCompletionSource<object?> playbackStarted,
        CancellationToken cancellationToken)
    {
        _ = playbackStarted.TrySetResult(null);
        await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, cancellationToken);
    }

    private static async Task SignalAndWaitAsync(
        TaskCompletionSource<object?> started,
        TaskCompletionSource<object?> release)
    {
        _ = started.TrySetResult(null);
        _ = await release.Task;
    }
}
