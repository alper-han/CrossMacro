using CrossMacro.UI.Services;
using FluentAssertions;
using Xunit;

namespace CrossMacro.UI.Tests.Services;

public class FileDialogFilterTests
{
    [Fact]
    public void NormalizePatterns_WhenNull_ReturnsEmpty()
    {
        var normalized = FileDialogFilter.NormalizePatterns(null);
        normalized.Should().BeEmpty();
    }

    [Theory]
    [InlineData("macro")]
    [InlineData(".macro")]
    [InlineData("*.macro")]
    [InlineData("*macro")]
    public void NormalizePatterns_AcceptsCommonExtensionFormats(string extension)
    {
        var normalized = FileDialogFilter.NormalizePatterns(new[] { extension });
        normalized.Should().Equal("*.macro");
    }

    [Fact]
    public void NormalizePatterns_RemovesDuplicatesCaseInsensitive()
    {
        var normalized = FileDialogFilter.NormalizePatterns(new[] { "macro", "*.MACRO", ".macro" });
        normalized.Should().Equal("*.macro");
    }
}
