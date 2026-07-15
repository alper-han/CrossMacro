namespace CrossMacro.Platform.Linux.Tests.DisplayServer;


public class IBusLayoutSourceTests
{
    [Fact]
    public void DetectLayout_ShouldNotThrow()
    {
        var source = new IBusLayoutSource();

        var ex = Record.Exception(source.DetectLayout);

        Assert.Null(ex);
    }
}
