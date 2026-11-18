
namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

public sealed class TextBufferStateTests
{
    private readonly TextBufferState _buffer;

    public TextBufferStateTests()
    {
        _buffer = new TextBufferState();
    }

    [Fact]
    public void Append_ShouldAddCharToBuffer()
    {
        _buffer.Append('a');
        _buffer.Append('b');
        _buffer.Append('c');

        // Assert - verify by matching
        var expansions = new List<TextExpansionEntry>
        {
            new() { IsEnabled = true, Trigger = "abc", Replacement = "test" },
        };

        _ = _buffer.TryGetMatch(expansions, out var match).Should().BeTrue();
        _ = match!.Trigger.Should().Be("abc");
    }

    [Fact]
    public void Backspace_ShouldRemoveLastChar()
    {
        _buffer.Append('a');
        _buffer.Append('b');
        _buffer.Append('c');

        _buffer.Backspace();

        // Assert - "ab" should remain, "abc" should not match
        var expansions = new List<TextExpansionEntry>
        {
            new() { IsEnabled = true, Trigger = "abc", Replacement = "test" },
            new() { IsEnabled = true, Trigger = "ab", Replacement = "test2" },
        };

        _ = _buffer.TryGetMatch(expansions, out var match).Should().BeTrue();
        _ = match!.Trigger.Should().Be("ab");
    }

    [Fact]
    public void Clear_ShouldEmptyBuffer()
    {
        _buffer.Append('x');
        _buffer.Append('y');

        _buffer.Clear();

        // Assert - no match possible after clear
        var expansions = new List<TextExpansionEntry>
        {
            new() { IsEnabled = true, Trigger = "xy", Replacement = "test" },
        };

        _ = _buffer.TryGetMatch(expansions, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetMatch_ShouldReturnTrue_WhenTriggerFound()
    {
        _buffer.Append('h');
        _buffer.Append('i');

        var expansions = new List<TextExpansionEntry>
        {
            new() { IsEnabled = true, Trigger = "hi", Replacement = "hello" },
        };

        var result = _buffer.TryGetMatch(expansions, out var match);

        _ = result.Should().BeTrue();
        _ = match.Should().NotBeNull();
        _ = match.Replacement.Should().Be("hello");
    }

    [Fact]
    public void TryGetMatch_ShouldReturnFalse_WhenNoTriggerFound()
    {
        _buffer.Append('x');
        _buffer.Append('y');

        var expansions = new List<TextExpansionEntry>
        {
            new() { IsEnabled = true, Trigger = "ab", Replacement = "test" },
        };

        var result = _buffer.TryGetMatch(expansions, out var match);

        _ = result.Should().BeFalse();
        _ = match.Should().BeNull();
    }

    [Fact]
    public void TryGetMatch_ShouldIgnoreDisabledExpansions()
    {
        _buffer.Append('h');
        _buffer.Append('i');

        var expansions = new List<TextExpansionEntry>
        {
            new() { IsEnabled = false, Trigger = "hi", Replacement = "hello" },
        };

        var result = _buffer.TryGetMatch(expansions, out _);

        _ = result.Should().BeFalse();
    }

    [Fact]
    public void TryGetMatch_ShouldMatchEndOfBuffer()
    {
        // Arrange - buffer contains "prefix:hi"
        foreach (var c in "prefix:hi")
        {
            _buffer.Append(c);
        }

        var expansions = new List<TextExpansionEntry>
        {
            new() { IsEnabled = true, Trigger = "hi", Replacement = "hello" },
        };

        var result = _buffer.TryGetMatch(expansions, out var match);

        // Assert - should match "hi" at end
        _ = result.Should().BeTrue();
        _ = match!.Trigger.Should().Be("hi");
    }

    [Fact]
    public void Backspace_ShouldDoNothing_WhenBufferEmpty()
    {
        // Act - should not throw
        _buffer.Backspace();
        _buffer.Backspace();

        // Assert - still empty
        var expansions = new List<TextExpansionEntry>
        {
            new() { IsEnabled = true, Trigger = "", Replacement = "test" },
        };

        _ = _buffer.TryGetMatch(expansions, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetMatch_WhenTriggersOverlap_PrefersTheLongestTriggerRegardlessOfListOrder()
    {
        var shortTrigger = new TextExpansionEntry(":a", "short");
        var longTrigger = new TextExpansionEntry("b:a", "long");
        foreach (var character in "b:a")
        {
            _buffer.Append(character);
        }

        _ = _buffer.TryGetMatch([shortTrigger, longTrigger], out var first).Should().BeTrue();
        _ = _buffer.TryGetMatch([longTrigger, shortTrigger], out var second).Should().BeTrue();

        _ = first.Should().BeSameAs(longTrigger);
        _ = second.Should().BeSameAs(longTrigger);
    }

    [Fact]
    public void TryGetMatch_ComparesOrdinally()
    {
        foreach (var character in "x:Mail")
        {
            _buffer.Append(character);
        }

        _ = _buffer.TryGetMatch([new TextExpansionEntry(":mail", "lower")], out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetMatch_MatchesTriggersLongerThanTheOldFiftyCharacterWindow()
    {
        var trigger = new string('t', 120);
        foreach (var character in "prefix" + trigger)
        {
            _buffer.Append(character);
        }

        _ = _buffer.TryGetMatch([new TextExpansionEntry(trigger, "long")], out var match).Should().BeTrue();

        _ = match!.Replacement.Should().Be("long");
    }
}
