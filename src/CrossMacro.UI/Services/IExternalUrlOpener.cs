namespace CrossMacro.UI.Services;

public interface IExternalUrlOpener
{
    public Task OpenAsync(Uri url);

    public Task OpenAsync(string url);
}
