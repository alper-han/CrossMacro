
namespace CrossMacro.UI.Tests.Services;

public sealed class FileDialogFilterTests
{
    [Fact]
    public void NormalizePatterns_WhenNull_ReturnsEmpty()
    {
        var normalized = FileDialogFilter.NormalizePatterns(extensions: null);
        _ = normalized.Should().BeEmpty();
    }

    [Theory]
    [InlineData("macro")]
    [InlineData(".macro")]
    [InlineData("*.macro")]
    [InlineData("*macro")]
    public void NormalizePatterns_AcceptsCommonExtensionFormats(string extension)
    {
        var normalized = FileDialogFilter.NormalizePatterns([extension]);
        _ = normalized.Should().Equal("*.macro");
    }

    [Fact]
    public void NormalizePatterns_RemovesDuplicatesCaseInsensitive()
    {
        var normalized = FileDialogFilter.NormalizePatterns(["macro", "*.MACRO", ".macro"]);
        _ = normalized.Should().Equal("*.macro");
    }

    [Fact]
    public void Extensions_AcceptsArrayAssignmentsThroughReadOnlyCollectionContract()
    {
        var filter = new FileDialogFilter { Extensions = ["macro", "png"] };

        _ = filter.Extensions.Should().Equal("macro", "png");
    }
}
