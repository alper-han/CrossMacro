using System;
using System.Collections.Generic;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class HotkeyMatcherTests
{
    private readonly HotkeyMatcher _matcher;

    public HotkeyMatcherTests()
    {
        _matcher = new HotkeyMatcher();
    }

    [Fact]
    public void TryMatch_ReturnsTrue_WhenKeyAndModifiersMatchExact()
    {
        // Arrange
        var mapping = new HotkeyMapping { MainKey = 30 }; // A
        mapping.RequiredModifiers.Add(29); // Ctrl

        var currentModifiers = new HashSet<int> { 29 };

        // Act
        var result = _matcher.TryMatch(30, currentModifiers, mapping, "Action");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void TryMatch_ReturnsFalse_WhenKeyDoesNotMatch()
    {
        // Arrange
        var mapping = new HotkeyMapping { MainKey = 30 };
        var currentModifiers = new HashSet<int>();

        // Act
        var result = _matcher.TryMatch(31, currentModifiers, mapping, "Action");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryMatch_ReturnsFalse_WhenModifierIsMissing()
    {
        // Arrange
        var mapping = new HotkeyMapping { MainKey = 30 };
        mapping.RequiredModifiers.Add(29); // Ctrl
        var currentModifiers = new HashSet<int>();

        // Act
        var result = _matcher.TryMatch(30, currentModifiers, mapping, "Action");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryMatch_ReturnsFalse_WhenExtraModifierIsPresent()
    {
        // Arrange
        var mapping = new HotkeyMapping { MainKey = 30 };
        // No required modifiers
        var currentModifiers = new HashSet<int> { 29 }; // Ctrl

        // Act
        var result = _matcher.TryMatch(30, currentModifiers, mapping, "Action");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryMatch_ReturnsTrue_WhenConfiguredPlaybackF9ReceivesNormalizedMacF9()
    {
        var mapping = new HotkeyMapping { MainKey = InputEventCode.KEY_F9 };
        var currentModifiers = new HashSet<int>();

        var result = _matcher.TryMatch(InputEventCode.KEY_F9, currentModifiers, mapping, "PlaybackF9MacRegression");

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(InputEventCode.KEY_F10)]
    [InlineData(InputEventCode.KEY_F21)]
    [InlineData(0)]
    [InlineData(InputEventCode.KEY_PLAYPAUSE)]
    public void TryMatch_ReturnsFalse_WhenConfiguredPlaybackF9ReceivesDifferentUnsupportedUnknownOrMediaKey(int keyCode)
    {
        var mapping = new HotkeyMapping { MainKey = InputEventCode.KEY_F9 };
        var currentModifiers = new HashSet<int>();

        var result = _matcher.TryMatch(keyCode, currentModifiers, mapping, "PlaybackF9RejectRegression");

        result.Should().BeFalse();
    }

    [Fact]
    public void TryMatch_ReturnsFalse_WhenDebounceIsActive()
    {
        // Arrange
        var mapping = new HotkeyMapping { MainKey = 30 };
        var currentModifiers = new HashSet<int>();
        var action = "DebounceTest";

        // Act
        var first = _matcher.TryMatch(30, currentModifiers, mapping, action);
        
        // Immediate second call
        var second = _matcher.TryMatch(30, currentModifiers, mapping, action);

        // Assert
        first.Should().BeTrue();
        second.Should().BeFalse("should be debounced");
    }
}
