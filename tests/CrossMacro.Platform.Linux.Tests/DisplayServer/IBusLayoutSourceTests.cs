namespace CrossMacro.Platform.Linux.Tests.DisplayServer;


public sealed class IBusLayoutSourceTests
{
    [Fact]
    public void ParseEngineOutput_WhenEnglishXkbEngine_ReturnsLanguageCode()
    {
        var layout = IBusLayoutSource.ParseEngineOutput("xkb:us::eng");

        Assert.Equal("us", layout);
    }

    [Fact]
    public void ParseEngineOutput_WhenTurkishXkbEngine_ReturnsLanguageCode()
    {
        var layout = IBusLayoutSource.ParseEngineOutput("xkb:tr::tur\n");

        Assert.Equal("tr", layout);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("simple")]
    [InlineData("xkb:")]
    public void ParseEngineOutput_WhenOutputIsMissingOrMalformed_ReturnsNull(string? output)
    {
        Assert.Null(IBusLayoutSource.ParseEngineOutput(output));
    }
}
