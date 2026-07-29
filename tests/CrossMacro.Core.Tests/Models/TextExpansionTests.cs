namespace CrossMacro.Core.Tests.Models;


public sealed class TextExpansionTests
{
    [Fact]
    public void NewTextExpansion_DefaultConstructor_HasEmptyValues()
    {
        // Arrange & Act
        var expansion = new TextExpansionEntry();

        // Assert
        _ = expansion.Trigger.Should().BeEmpty();
        _ = expansion.Replacement.Should().BeEmpty();
        _ = expansion.IsEnabled.Should().BeTrue();
        _ = expansion.Method.Should().Be(PasteMethod.CtrlV);
        _ = expansion.InsertionMode.Should().Be(TextInsertionMode.Paste);
    }

    [Fact]
    public void TextExpansion_ParameterizedConstructor_SetsAllValues()
    {
        // Arrange & Act
        var expansion = new TextExpansionEntry(
            ":mail",
            "test@example.com",
isEnabled: true,
            PasteMethod.CtrlShiftV,
            TextInsertionMode.DirectTyping);

        // Assert
        _ = expansion.Trigger.Should().Be(":mail");
        _ = expansion.Replacement.Should().Be("test@example.com");
        _ = expansion.IsEnabled.Should().BeTrue();
        _ = expansion.Method.Should().Be(PasteMethod.CtrlShiftV);
        _ = expansion.InsertionMode.Should().Be(TextInsertionMode.DirectTyping);
    }

    [Fact]
    public void TextExpansion_ParameterizedConstructor_CanBeDisabled()
    {
        // Arrange & Act
        var expansion = new TextExpansionEntry(":sig", "Best regards,\nJohn", isEnabled: false);

        // Assert
        _ = expansion.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void TextExpansion_CanSetTrigger()
    {
        // Arrange
        var expansion = new TextExpansionEntry
        {
            // Act
            Trigger = ":addr",
        };

        // Assert
        _ = expansion.Trigger.Should().Be(":addr");
    }

    [Fact]
    public void TextExpansion_CanSetReplacement()
    {
        // Arrange
        var expansion = new TextExpansionEntry
        {
            // Act
            Replacement = "123 Main Street, City, Country",
        };

        // Assert
        _ = expansion.Replacement.Should().Be("123 Main Street, City, Country");
    }

    [Fact]
    public void TextExpansion_CanToggleEnabled()
    {
        // Arrange
        var expansion = new TextExpansionEntry(":test", "test")
        {
            // Act
            IsEnabled = false,
        };

        // Assert
        _ = expansion.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void TextExpansion_SupportsMultilineReplacement()
    {
        // Arrange
        const string multilineText = "Line 1\nLine 2\nLine 3";

        // Act
        var expansion = new TextExpansionEntry(":multi", multilineText);

        // Assert
        _ = expansion.Replacement.Should().Contain("\n");
        _ = expansion.Replacement.Should().Be(multilineText);
    }

    [Fact]
    public void TextExpansion_SupportsSpecialCharactersInTrigger()
    {
        // Arrange & Act
        var expansion = new TextExpansionEntry("::email", "user@domain.com");

        // Assert
        _ = expansion.Trigger.Should().Be("::email");
    }

    [Fact]
    public void TextExpansion_SupportsUnicodeInReplacement()
    {
        // Arrange
        const string unicodeText = "こんにちは 🎉 Привет";

        // Act
        var expansion = new TextExpansionEntry(":hello", unicodeText);

        // Assert
        _ = expansion.Replacement.Should().Be(unicodeText);
    }

    [Theory]
    [InlineData(":a", "Alpha")]
    [InlineData(":brb", "Be right back")]
    [InlineData(":shrug", "¯\\_(ツ)_/¯")]
    [InlineData(":date", "2024-12-25")]
    public void TextExpansion_SupportsVariousTriggerPatterns(string trigger, string replacement)
    {
        // Arrange & Act
        var expansion = new TextExpansionEntry(trigger, replacement);

        // Assert
        _ = expansion.Trigger.Should().Be(trigger);
        _ = expansion.Replacement.Should().Be(replacement);
    }
}
