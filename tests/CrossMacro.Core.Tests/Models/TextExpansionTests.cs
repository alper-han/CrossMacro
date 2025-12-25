namespace CrossMacro.Core.Tests.Models;

using CrossMacro.Core.Models;
using FluentAssertions;

public class TextExpansionTests
{
    [Fact]
    public void NewTextExpansion_DefaultConstructor_HasEmptyValues()
    {
        // Arrange & Act
        var expansion = new TextExpansion();

        // Assert
        expansion.Trigger.Should().BeEmpty();
        expansion.Replacement.Should().BeEmpty();
        expansion.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void TextExpansion_ParameterizedConstructor_SetsAllValues()
    {
        // Arrange & Act
        var expansion = new TextExpansion(":mail", "test@example.com", true);

        // Assert
        expansion.Trigger.Should().Be(":mail");
        expansion.Replacement.Should().Be("test@example.com");
        expansion.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void TextExpansion_ParameterizedConstructor_CanBeDisabled()
    {
        // Arrange & Act
        var expansion = new TextExpansion(":sig", "Best regards,\nJohn", false);

        // Assert
        expansion.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void TextExpansion_CanSetTrigger()
    {
        // Arrange
        var expansion = new TextExpansion();

        // Act
        expansion.Trigger = ":addr";

        // Assert
        expansion.Trigger.Should().Be(":addr");
    }

    [Fact]
    public void TextExpansion_CanSetReplacement()
    {
        // Arrange
        var expansion = new TextExpansion();

        // Act
        expansion.Replacement = "123 Main Street, City, Country";

        // Assert
        expansion.Replacement.Should().Be("123 Main Street, City, Country");
    }

    [Fact]
    public void TextExpansion_CanToggleEnabled()
    {
        // Arrange
        var expansion = new TextExpansion(":test", "test");

        // Act
        expansion.IsEnabled = false;

        // Assert
        expansion.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void TextExpansion_SupportsMultilineReplacement()
    {
        // Arrange
        var multilineText = "Line 1\nLine 2\nLine 3";

        // Act
        var expansion = new TextExpansion(":multi", multilineText);

        // Assert
        expansion.Replacement.Should().Contain("\n");
        expansion.Replacement.Should().Be(multilineText);
    }

    [Fact]
    public void TextExpansion_SupportsSpecialCharactersInTrigger()
    {
        // Arrange & Act
        var expansion = new TextExpansion("::email", "user@domain.com");

        // Assert
        expansion.Trigger.Should().Be("::email");
    }

    [Fact]
    public void TextExpansion_SupportsUnicodeInReplacement()
    {
        // Arrange
        var unicodeText = "こんにちは 🎉 Привет";

        // Act
        var expansion = new TextExpansion(":hello", unicodeText);

        // Assert
        expansion.Replacement.Should().Be(unicodeText);
    }

    [Theory]
    [InlineData(":a", "Alpha")]
    [InlineData(":brb", "Be right back")]
    [InlineData(":shrug", "¯\\_(ツ)_/¯")]
    [InlineData(":date", "2024-12-25")]
    public void TextExpansion_SupportsVariousTriggerPatterns(string trigger, string replacement)
    {
        // Arrange & Act
        var expansion = new TextExpansion(trigger, replacement);

        // Assert
        expansion.Trigger.Should().Be(trigger);
        expansion.Replacement.Should().Be(replacement);
    }
}
