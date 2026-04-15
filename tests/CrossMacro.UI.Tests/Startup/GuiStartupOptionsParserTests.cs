using CrossMacro.UI.Startup;

namespace CrossMacro.UI.Tests.Startup;

public class GuiStartupOptionsParserTests
{
    [Fact]
    public void Parse_WhenNoArgs_ReturnsDefaultOptions()
    {
        var result = GuiStartupOptionsParser.Parse([]);

        Assert.False(result.Options.StartMinimized);
        Assert.Empty(result.ForwardedArgs);
    }

    [Fact]
    public void Parse_WhenStartMinimizedFlagPresent_SetsOptionAndStripsFlag()
    {
        var result = GuiStartupOptionsParser.Parse(["--start-minimized", "--display=:0"]);

        Assert.True(result.Options.StartMinimized);
        Assert.Equal(["--display=:0"], result.ForwardedArgs);
    }

    [Fact]
    public void Parse_WhenUnknownArgsPresent_PreservesOriginalOrder()
    {
        var result = GuiStartupOptionsParser.Parse(["--display=:0", "--start-minimized", "file.txt"]);

        Assert.True(result.Options.StartMinimized);
        Assert.Equal(["--display=:0", "file.txt"], result.ForwardedArgs);
    }
}
