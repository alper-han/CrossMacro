namespace CrossMacro.Core.Tests.Services;

using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
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
}
