namespace CrossMacro.Core.Tests.Services;

using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using FluentAssertions;
using NSubstitute;

/// <summary>
/// Tests for MacroRecorder focusing on initialization and error handling
/// </summary>
public class MacroRecorderTests
{
    private readonly IMousePositionProvider _positionProvider;

    public MacroRecorderTests()
    {
        _positionProvider = Substitute.For<IMousePositionProvider>();
        _positionProvider.IsSupported.Returns(true);
    }

    [Fact]
    public void IsRecording_Initially_IsFalse()
    {
        // Arrange
        var recorder = new MacroRecorder(_positionProvider);

        // Assert
        recorder.IsRecording.Should().BeFalse();
    }

    [Fact]
    public async Task StartRecordingAsync_NoMouseNoKeyboard_ThrowsArgumentException()
    {
        // Arrange
        var recorder = new MacroRecorder(_positionProvider);

        // Act
        var act = async () => await recorder.StartRecordingAsync(
            recordMouse: false, 
            recordKeyboard: false);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*at least one*");
    }

    [Fact]
    public void StopRecording_WhenNotRecording_ThrowsInvalidOperationException()
    {
        // Arrange
        var recorder = new MacroRecorder(_positionProvider);

        // Act
        var act = () => recorder.StopRecording();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Not currently recording*");
    }

    [Fact]
    public void GetCurrentRecording_WhenNotRecording_ReturnsNull()
    {
        // Arrange
        var recorder = new MacroRecorder(_positionProvider);

        // Act
        var result = recorder.GetCurrentRecording();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var recorder = new MacroRecorder(_positionProvider);

        // Act
        var act = () =>
        {
            recorder.Dispose();
            recorder.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithUnsupportedPositionProvider_DoesNotThrow()
    {
        // Arrange
        var unsupportedProvider = Substitute.For<IMousePositionProvider>();
        unsupportedProvider.IsSupported.Returns(false);

        // Act
        var act = () => new MacroRecorder(unsupportedProvider);

        // Assert
        act.Should().NotThrow();
    }
}
