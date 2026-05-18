using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services.Playback;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.Core.Tests.Services.Playback;

public class DefaultPlaybackCoordinatorTests
{
    [Fact]
    public async Task InitializeAsync_AbsoluteMode_DoesNotPreMoveFirstEvent()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            [
                new MacroEvent { Type = EventType.MouseMove, X = 120, Y = 90 }
            ]
        };

        // Act
        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        coordinator.CurrentX.Should().Be(0);
        coordinator.CurrentY.Should().Be(0);
    }

    [Fact]
    public async Task InitializeAsync_AbsoluteMode_WithPositionProvider_TracksCurrentPositionWithoutPreMove()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var positionProvider = Substitute.For<IMousePositionProvider>();
        positionProvider.IsSupported.Returns(true);
        positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>((50, 40)));
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            [
                new MacroEvent { Type = EventType.MouseMove, X = 120, Y = 90 }
            ]
        };

        // Act
        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        coordinator.CurrentX.Should().Be(50);
        coordinator.CurrentY.Should().Be(40);
    }

    [Fact]
    public async Task PrepareIterationAsync_AbsoluteMode_DoesNotPreMoveFirstEvent()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        coordinator.UpdatePosition(50, 40);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            [
                new MacroEvent { Type = EventType.MouseMove, X = 200, Y = 150 }
            ]
        };

        // Act
        await coordinator.PrepareIterationAsync(1, macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        coordinator.CurrentX.Should().Be(50);
        coordinator.CurrentY.Should().Be(40);
    }

    [Fact]
    public async Task InitializeAsync_RelativeCurrentPositionMacro_DoesNotCornerResetEvenWhenSkipIsFalse()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = false,
            Events =
            [
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MouseButton.Left,
                    X = 0,
                    Y = 0,
                    UseCurrentPosition = true
                }
            ]
        };

        // Act
        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveRelative(-20000, -20000);
    }

    [Fact]
    public async Task InitializeAsync_RelativeMacroWithLaterCurrentPositionEvent_StillPerformsCornerReset()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = false,
            Events =
            [
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative
                },
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MouseButton.Left,
                    UseCurrentPosition = true
                }
            ]
        };

        // Act
        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.Received(1).MoveRelative(-20000, -20000);
    }

    [Fact]
    public async Task PrepareIterationAsync_RelativeCurrentPositionMacro_DoesNotCornerResetEvenWhenSkipIsFalse()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = false,
            Events =
            [
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MouseButton.Left,
                    X = 0,
                    Y = 0,
                    UseCurrentPosition = true
                }
            ]
        };

        // Act
        await coordinator.PrepareIterationAsync(1, macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveRelative(-20000, -20000);
    }

    [Fact]
    public async Task InitializeAsync_AbsoluteCurrentPositionClick_DoesNotMoveToStoredCoordinates()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            [
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MouseButton.Left,
                    X = 800,
                    Y = 600,
                    UseCurrentPosition = true
                }
            ]
        };

        // Act
        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task InitializeAsync_AbsoluteLeadingCurrentPositionClick_DoesNotPreMoveToLaterAbsoluteEvent()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            [
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MouseButton.Left,
                    X = 0,
                    Y = 0,
                    UseCurrentPosition = true
                },
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 900,
                    Y = 700
                }
            ]
        };

        // Act
        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task PrepareIterationAsync_AbsoluteLeadingCurrentPositionClick_DoesNotPreMoveToLaterAbsoluteEvent()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        coordinator.UpdatePosition(300, 200);
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            [
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MouseButton.Left,
                    X = 0,
                    Y = 0,
                    UseCurrentPosition = true
                },
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 900,
                    Y = 700
                }
            ]
        };

        // Act
        await coordinator.PrepareIterationAsync(1, macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task InitializeAsync_LegacyRelativeMacroWithExplicitAbsoluteFirstEvent_DoesNotPreMoveFirstEvent()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events =
            [
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 300,
                    Y = 250,
                    CoordinateMode = MouseCoordinateMode.Absolute
                }
            ]
        };

        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(-20000, -20000);
        coordinator.CurrentX.Should().Be(0);
        coordinator.CurrentY.Should().Be(0);
    }

    [Fact]
    public async Task InitializeAsync_LegacyAbsoluteMacroWithExplicitRelativeFirstEvent_UsesRelativePreparation()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            SkipInitialZeroZero = false,
            Events =
            [
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative
                }
            ]
        };

        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        simulator.Received(1).MoveRelative(-20000, -20000);
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task PrepareIterationAsync_LegacyRelativeMacroWithExplicitAbsoluteFirstEvent_DoesNotPreMoveFirstEvent()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        coordinator.UpdatePosition(20, 30);
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events =
            [
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 300,
                    Y = 250,
                    CoordinateMode = MouseCoordinateMode.Absolute
                }
            ]
        };

        await coordinator.PrepareIterationAsync(1, macro, simulator, 1920, 1080, CancellationToken.None);

        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        coordinator.CurrentX.Should().Be(20);
        coordinator.CurrentY.Should().Be(30);
    }
}
