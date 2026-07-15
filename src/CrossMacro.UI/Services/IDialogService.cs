
namespace CrossMacro.UI.Services;

public interface IDialogService
{
    public Task<bool> ShowConfirmationAsync(string title, string message, string yesText = "Yes", string noText = "No");
    public Task ShowMessageAsync(string title, string message, string buttonText = "OK");

    public Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, FileDialogFilter[] filters);
    public Task<string?> ShowOpenFileDialogAsync(string title, FileDialogFilter[] filters);
}
