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
        _ = _viewModel.CanRunSelectedTest.Should().BeFalse();
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

    [Fact]
    public async Task ToggleTestPlaybackSelectedAsync_WhenNoActionsSelected_SetsStatusAndDoesNotPlay()
    {
        // Arrange
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseClick, X = 10, Y = 20 });
        _viewModel.SelectedActionUnderlyingIndices.Clear();

        // Act
        await _viewModel.ToggleTestPlaybackSelectedAsync();

        // Assert
        _ = _viewModel.Status.Should().Be("Editor_StatusSelectActionFirst");
        await _macroPlayer.DidNotReceive().PlayAsync(
            Arg.Any<MacroSequence>(),
            Arg.Any<PlaybackOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleTestPlaybackSelectedAsync_WhenActionsSelected_PlaysOnlySelectedActions()
    {
        // Arrange
        var action1 = new EditorAction { Type = EditorActionType.MouseClick, X = 10, Y = 20 };
        var action2 = new EditorAction { Type = EditorActionType.Delay, DelayMs = 150 };
        var action3 = new EditorAction { Type = EditorActionType.KeyDown, KeyCode = 65 };
        _viewModel.Actions.Add(action1);
        _viewModel.Actions.Add(action2);
        _viewModel.Actions.Add(action3);

        _viewModel.SelectedActionUnderlyingIndices.Clear();
        _viewModel.SelectedActionUnderlyingIndices.Add(1);

        EditorMacroProjection? capturedProjection = null;
        _ = _converter
            .ToMacroSequence(Arg.Do<EditorMacroProjection>(p => capturedProjection = p))
            .Returns(new MacroSequence
            {
                Events = { new MacroEvent { Type = EventType.Click, X = 10, Y = 20 } },
            });
        _ = _macroPlayer
            .PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _viewModel.ToggleTestPlaybackSelectedAsync();

        // Assert
        _ = _viewModel.IsRunningSelectedTest.Should().BeFalse();
        _ = _viewModel.Status.Should().Be("Editor_StatusTestSelectedComplete");
        _ = capturedProjection.Should().NotBeNull();
        _ = capturedProjection?.Actions.Should().HaveCount(1);
        _ = capturedProjection?.Actions[0].Type.Should().Be(EditorActionType.Delay);
        _ = capturedProjection?.Actions[0].DelayMs.Should().Be(150);
        await _macroPlayer.Received(1).PlayAsync(
            Arg.Any<MacroSequence>(),
            Arg.Any<PlaybackOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleTestPlaybackSelectedAsync_WhenRunning_StopsAndReportsCancellation()
    {
        // Arrange
        var action = new EditorAction { Type = EditorActionType.MouseClick, X = 10, Y = 20 };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedActionUnderlyingIndices.Clear();
        _viewModel.SelectedActionUnderlyingIndices.Add(0);

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
        var playbackTask = _viewModel.ToggleTestPlaybackSelectedAsync();
        _ = await playbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, CancellationToken.None);
        _ = _viewModel.IsRunningSelectedTest.Should().BeTrue();
        _ = _viewModel.CanRunTest.Should().BeFalse();

        await _viewModel.ToggleTestPlaybackSelectedAsync();
        await playbackTask;

        // Assert
        _ = _viewModel.Status.Should().Be("Editor_StatusTestCancelled");
        _ = _viewModel.IsRunningSelectedTest.Should().BeFalse();
        _macroPlayer.Received(1).StopPlayback();
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
