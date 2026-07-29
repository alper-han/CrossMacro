
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignExternalUrlOpener : IExternalUrlOpener
{
    public Task OpenAsync(Uri url) => Task.CompletedTask;

    public Task OpenAsync(string url) => Task.CompletedTask;
}
