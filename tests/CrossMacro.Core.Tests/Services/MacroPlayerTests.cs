namespace CrossMacro.Core.Tests.Services;

using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Playback;
using FluentAssertions;
using NSubstitute;

/// <summary>
/// Tests for MacroPlayer focusing on edge cases and error handling
/// </summary>
public class MacroPlayerTests
{
    private readonly IMousePositionProvider _positionProvider;
    private readonly PlaybackValidator _validator;

    public MacroPlayerTests()
    {
        _positionProvider = Substitute.For<IMousePositionProvider>();
        _positionProvider.IsSupported.Returns(true);
        _positionProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));
        _validator = new PlaybackValidator(_positionProvider);
    }

    [Fact]
    public async Task PlayAsync_NullMacro_ThrowsArgumentNullException()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Act
        var act = async () => await player.PlayAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PlayAsync_EmptyMacro_ThrowsInvalidOperationException()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);
        var macro = new MacroSequence(); // Empty events

        // Act
        var act = async () => await player.PlayAsync(macro);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*validation failed*");
    }

    [Fact]
    public void IsPlaying_Initially_IsFalse()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Assert
        player.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public void IsPaused_Initially_IsFalse()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Assert
        player.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void CurrentLoop_Initially_IsZero()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Assert
        player.CurrentLoop.Should().Be(0);
    }

    [Fact]
    public void TotalLoops_Initially_IsZero()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Assert
        player.TotalLoops.Should().Be(0);
    }

    [Fact]
    public void Stop_WhenNotPlaying_DoesNotThrow()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Act
        var act = () => player.Stop();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Pause_WhenNotPlaying_DoesNothing()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Act
        player.Pause();

        // Assert
        player.IsPaused.Should().BeFalse(); // Can't pause when not playing
    }

    [Fact]
    public void Resume_WhenNotPlaying_DoesNothing()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Act
        player.Resume();

        // Assert
        player.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Act
        var act = () =>
        {
            player.Dispose();
            player.Dispose();
            player.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task PlayAsync_ExecutesEvents_OnInputSimulator()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        
        var player = new MacroPlayer(
            _positionProvider, 
            _validator, 
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 100, Y = 100 },
                new() { Type = EventType.ButtonPress, Button = MouseButton.Left },
                new() { Type = EventType.KeyPress, KeyCode = 30 }
            }
        };

        // Act
        await player.PlayAsync(macro);

        // Assert
        // Verify MoveRelative (default mode)
        simulator.Received().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        
        // Verify MouseButton
        simulator.Received().MouseButton(Arg.Any<int>(), true);
        
        // Verify KeyPress
        simulator.Received().KeyPress(30, true);
    }

    [Fact]
    public async Task PlayAsync_WhenLooping_UsesRepeatDelayFromOptions()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timing,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 }
            }
        };

        var options = new PlaybackOptions
        {
            Loop = true,
            RepeatCount = 2,
            RepeatDelayMs = 123
        };

        // Act
        await player.PlayAsync(macro, options);

        // Assert
        timing.WaitCalls.Should().Contain(123);
    }

    [Fact]
    public async Task PlayAsync_WhenEventHasRandomDelay_UsesFixedPlusRandomDelay()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timing,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 20,
                    Y = 20,
                    DelayMs = 30,
                    HasRandomDelay = true,
                    RandomDelayMinMs = 20,
                    RandomDelayMaxMs = 20
                }
            }
        };

        // Act
        await player.PlayAsync(macro);

        // Assert
        timing.WaitCalls.Should().Contain(50);
    }

    [Fact]
    public async Task PlayAsync_WhenMacroHasTrailingRandomDelay_UsesFixedPlusRandomDelay()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timing,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            TrailingDelayMs = 15,
            HasTrailingRandomDelay = true,
            TrailingDelayMinMs = 25,
            TrailingDelayMaxMs = 25,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 }
            }
        };

        // Act
        await player.PlayAsync(macro);

        // Assert
        timing.WaitCalls.Should().Contain(40);
    }

    [Fact]
    public async Task PlayAsync_WhenAlreadyPlaying_ThrowsInvalidOperationException()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timing,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 1 }
            }
        };

        // Block timing service so first playback remains in-progress.
        timing.WaitEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        timing.ContinueWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstPlayback = player.PlayAsync(macro);
        await timing.WaitEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Act
        var act = async () => await player.PlayAsync(macro);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already in progress*");

        player.Stop();
        timing.ContinueWait.TrySetResult(true);
        await firstPlayback;
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-3.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task PlayAsync_WhenSpeedMultiplierIsInvalid_NormalizesAndPlays(double speedMultiplier)
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");

        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 100 }
            }
        };

        var options = new PlaybackOptions
        {
            SpeedMultiplier = speedMultiplier
        };

        // Act
        var act = async () => await player.PlayAsync(macro, options);

        // Assert
        await act.Should().NotThrowAsync();
        simulator.Received().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }

    private sealed class RecordingTimingService : IPlaybackTimingService
    {
        public List<int> WaitCalls { get; } = new();
        public TaskCompletionSource<bool>? WaitEntered { get; set; }
        public TaskCompletionSource<bool>? ContinueWait { get; set; }

        public async Task WaitAsync(int delayMs, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken)
        {
            WaitCalls.Add(delayMs);
            WaitEntered?.TrySetResult(true);

            if (ContinueWait != null)
            {
                await ContinueWait.Task.WaitAsync(cancellationToken);
            }

            if (pauseToken.IsPaused)
            {
                await pauseToken.WaitIfPausedAsync(cancellationToken);
            }
        }
    }
}
