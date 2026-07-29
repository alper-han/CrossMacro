
namespace CrossMacro.Infrastructure.Tests.Services;

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
        // Act
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
        // Arrange
        _buffer.Append('a');
        _buffer.Append('b');
        _buffer.Append('c');

        // Act
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
        // Arrange
        _buffer.Append('x');
        _buffer.Append('y');

        // Act
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
        // Arrange
        _buffer.Append('h');
        _buffer.Append('i');

        var expansions = new List<TextExpansionEntry>
        {
            new() { IsEnabled = true, Trigger = "hi", Replacement = "hello" },
        };

        // Act
        var result = _buffer.TryGetMatch(expansions, out var match);

        // Assert
        _ = result.Should().BeTrue();
        _ = match.Should().NotBeNull();
        _ = match!.Replacement.Should().Be("hello");
    }

    [Fact]
    public void TryGetMatch_ShouldReturnFalse_WhenNoTriggerFound()
    {
        // Arrange
        _buffer.Append('x');
        _buffer.Append('y');

        var expansions = new List<TextExpansionEntry>
        {
            new() { IsEnabled = true, Trigger = "ab", Replacement = "test" },
        };

        // Act
        var result = _buffer.TryGetMatch(expansions, out var match);

        // Assert
        _ = result.Should().BeFalse();
        _ = match.Should().BeNull();
    }

    [Fact]
    public void TryGetMatch_ShouldIgnoreDisabledExpansions()
    {
        // Arrange
        _buffer.Append('h');
        _buffer.Append('i');

        var expansions = new List<TextExpansionEntry>
        {
            new() { IsEnabled = false, Trigger = "hi", Replacement = "hello" },
        };

        // Act
        var result = _buffer.TryGetMatch(expansions, out _);

        // Assert
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

        // Act
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
}
