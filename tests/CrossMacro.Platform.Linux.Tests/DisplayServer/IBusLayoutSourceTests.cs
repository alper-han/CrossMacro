namespace CrossMacro.Platform.Linux.Tests.DisplayServer;


public sealed class IBusLayoutSourceTests
{
    [Fact]
    public void DetectLayout_ShouldNotThrow()
    {
        var ex = Record.Exception(IBusLayoutSource.DetectLayout);

        Assert.Null(ex);
    }
}
