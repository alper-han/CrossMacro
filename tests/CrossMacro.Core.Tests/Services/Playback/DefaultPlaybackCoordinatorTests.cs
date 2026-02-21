using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Playback;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.Core.Tests.Services.Playback;

public class DefaultPlaybackCoordinatorTests
{
    [Fact]
    public async Task InitializeAsync_AbsoluteMode_WhenRelativePreferenceEnabled_UsesRelativeMove()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator(preferRelativeForAbsoluteMoves: true);
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
        simulator.Received(1).MoveRelative(120, 90);
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        coordinator.CurrentX.Should().Be(120);
        coordinator.CurrentY.Should().Be(90);
    }

    [Fact]
    public async Task InitializeAsync_AbsoluteMode_WhenRelativePreferenceDisabled_UsesAbsoluteMove()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator(preferRelativeForAbsoluteMoves: false);
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
        simulator.Received(1).MoveAbsolute(120, 90);
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        coordinator.CurrentX.Should().Be(120);
        coordinator.CurrentY.Should().Be(90);
    }

    [Fact]
    public async Task PrepareIterationAsync_AbsoluteMode_WhenRelativePreferenceDisabled_UsesAbsoluteMove()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator(preferRelativeForAbsoluteMoves: false);
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
        simulator.Received(1).MoveAbsolute(200, 150);
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        coordinator.CurrentX.Should().Be(200);
        coordinator.CurrentY.Should().Be(150);
    }
}
