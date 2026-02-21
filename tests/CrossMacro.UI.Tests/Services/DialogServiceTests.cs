namespace CrossMacro.UI.Tests.Services;

using System.Threading.Tasks;
using CrossMacro.UI.Services;

public class DialogServiceTests
{
    [Fact(Timeout = 5000)]
    public async Task ShowConfirmationAsync_WhenNoDesktopLifetime_ReturnsFalse()
    {
        var service = new DialogService();

        var result = await service.ShowConfirmationAsync("Title", "Message");

        Assert.False(result);
    }

    [Fact(Timeout = 5000)]
    public async Task ShowMessageAsync_WhenNoDesktopLifetime_DoesNotThrow()
    {
        var service = new DialogService();

        var ex = await Record.ExceptionAsync(() => service.ShowMessageAsync("Title", "Message"));

        Assert.Null(ex);
    }

    [Fact(Timeout = 5000)]
    public async Task ShowSaveFileDialogAsync_WhenNoMainWindow_ReturnsNull()
    {
        var service = new DialogService();

        var result = await service.ShowSaveFileDialogAsync("Save", "macro.macro", []);

        Assert.Null(result);
    }

    [Fact(Timeout = 5000)]
    public async Task ShowOpenFileDialogAsync_WhenNoMainWindow_ReturnsNull()
    {
        var service = new DialogService();

        var result = await service.ShowOpenFileDialogAsync("Open", []);

        Assert.Null(result);
    }
}
