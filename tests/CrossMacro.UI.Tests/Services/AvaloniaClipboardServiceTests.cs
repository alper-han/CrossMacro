namespace CrossMacro.UI.Tests.Services;

using CrossMacro.UI.Services;

public class AvaloniaClipboardServiceTests
{
    [Fact(Timeout = 5000)]
    public async Task SetTextAsync_WhenNoApplicationCurrent_ShouldNotThrow()
    {
        var service = new AvaloniaClipboardService();

        var ex = await Record.ExceptionAsync(() => service.SetTextAsync("hello"));

        Assert.Null(ex);
    }

    [Fact(Timeout = 5000)]
    public async Task GetTextAsync_WhenNoApplicationCurrent_ShouldReturnNull()
    {
        var service = new AvaloniaClipboardService();

        var result = await service.GetTextAsync();

        Assert.Null(result);
    }
}
