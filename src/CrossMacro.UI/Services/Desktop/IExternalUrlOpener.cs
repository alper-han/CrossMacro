namespace CrossMacro.UI.Services.Desktop;

public interface IExternalUrlOpener
{
    public Task OpenAsync(Uri url);

    public Task OpenAsync(string url);
}
