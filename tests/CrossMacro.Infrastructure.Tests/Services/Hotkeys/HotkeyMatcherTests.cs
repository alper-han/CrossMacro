
using System.Globalization;

namespace CrossMacro.Infrastructure.Tests.Services.Hotkeys;

public sealed class HotkeyMatcherTests
{
    private readonly HotkeyMatcher _matcher;

    public HotkeyMatcherTests()
    {
        _matcher = new HotkeyMatcher();
    }

    [Fact]
    public void TryMatch_ReturnsTrue_WhenKeyAndModifiersMatchExact()
    {
        var mapping = new HotkeyMapping { MainKey = 30 }; // A
        _ = mapping.RequiredModifiers.Add(29); // Ctrl

        var currentModifiers = new HashSet<int> { 29 };

        var result = _matcher.TryMatch(30, currentModifiers, mapping, "Action");

        _ = result.Should().BeTrue();
    }

    [Fact]
    public void TryMatch_ReturnsFalse_WhenKeyDoesNotMatch()
    {
        var mapping = new HotkeyMapping { MainKey = 30 };
        var currentModifiers = new HashSet<int>();

        var result = _matcher.TryMatch(31, currentModifiers, mapping, "Action");

        _ = result.Should().BeFalse();
    }

    [Fact]
    public void TryMatch_ReturnsFalse_WhenModifierIsMissing()
    {
        var mapping = new HotkeyMapping { MainKey = 30 };
        _ = mapping.RequiredModifiers.Add(29); // Ctrl
        var currentModifiers = new HashSet<int>();

        var result = _matcher.TryMatch(30, currentModifiers, mapping, "Action");

        _ = result.Should().BeFalse();
    }

    [Fact]
    public void TryMatch_ReturnsFalse_WhenExtraModifierIsPresent()
    {
        var mapping = new HotkeyMapping { MainKey = 30 };
        // No required modifiers
        var currentModifiers = new HashSet<int> { 29 }; // Ctrl

        var result = _matcher.TryMatch(30, currentModifiers, mapping, "Action");

        _ = result.Should().BeFalse();
    }

    [Fact]
    public void TryMatch_ReturnsTrue_WhenConfiguredPlaybackF9ReceivesNormalizedMacF9()
    {
        var mapping = new HotkeyMapping { MainKey = InputEventCode.KEY_F9 };
        var currentModifiers = new HashSet<int>();

        var result = _matcher.TryMatch(InputEventCode.KEY_F9, currentModifiers, mapping, "PlaybackF9MacRegression");

        _ = result.Should().BeTrue();
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

        _ = result.Should().BeFalse();
    }

    [Fact]
    public void TryMatch_ReturnsFalse_WhenDebounceIsActive()
    {
        var mapping = new HotkeyMapping { MainKey = 30 };
        var currentModifiers = new HashSet<int>();
        const string action = "DebounceTest";

        var first = _matcher.TryMatch(30, currentModifiers, mapping, action);

        // Immediate second call
        var second = _matcher.TryMatch(30, currentModifiers, mapping, action);

        _ = first.Should().BeTrue();
        _ = second.Should().BeFalse("should be debounced");
    }

    [Fact]
    public void TryMatch_UsesInjectedTimeProviderForDebounceBoundary()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z", CultureInfo.InvariantCulture));
        var matcher = new HotkeyMatcher(timeProvider);
        var mapping = new HotkeyMapping { MainKey = 30 };
        var modifiers = new HashSet<int>();

        Assert.True(matcher.TryMatch(30, modifiers, mapping, "DeterministicDebounce"));
        timeProvider.Advance(TimeSpan.FromMilliseconds(299));
        Assert.False(matcher.TryMatch(30, modifiers, mapping, "DeterministicDebounce"));
        timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(matcher.TryMatch(30, modifiers, mapping, "DeterministicDebounce"));
    }
}
