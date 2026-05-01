namespace CrossMacro.UI.Tests.Services;

using CrossMacro.UI.Services;
using NSubstitute;

public class AvaloniaClipboardServiceTests
{
    private static readonly IDesktopLifetimeContext NoDesktopLifetime = Substitute.For<IDesktopLifetimeContext>();

    [Fact(Timeout = 5000)]
    public async Task SetTextAsync_WhenNoApplicationCurrent_ShouldNotThrow()
    {
        var service = new AvaloniaClipboardService(NoDesktopLifetime);

        var ex = await Record.ExceptionAsync(() => service.SetTextAsync("hello"));

        Assert.Null(ex);
    }

    [Fact(Timeout = 5000)]
    public async Task GetTextAsync_WhenNoApplicationCurrent_ShouldReturnNull()
    {
        var service = new AvaloniaClipboardService(NoDesktopLifetime);

        var result = await service.GetTextAsync();

        Assert.Null(result);
    }
}
