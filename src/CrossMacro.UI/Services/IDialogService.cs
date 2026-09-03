
namespace CrossMacro.UI.Services;

public interface IDialogService
{
    public Task<bool> ShowConfirmationAsync(string title, string message, string yesText = "Yes", string noText = "No");
    public Task ShowMessageAsync(string title, string message, string buttonText = "OK");

    public Task<UnsavedChangesChoice> ShowUnsavedChangesAsync(
        string title,
        string message,
        string saveText,
        string discardText,
        string cancelText) => Task.FromResult(UnsavedChangesChoice.Cancel);

    public Task<FastLoopWarningResult> ShowFastLoopWarningAsync(
        string title,
        string message,
        string continueText,
        string cancelText,
        string suppressText) => Task.FromResult(FastLoopWarningResult.Cancelled);

    public Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, FileDialogFilter[] filters);
    public Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, ReadOnlyMemory<FileDialogFilter> filters) =>
        ShowSaveFileDialogAsync(title, defaultFileName, filters.ToArray());

    public Task<string?> ShowOpenFileDialogAsync(string title, FileDialogFilter[] filters);
    public Task<string?> ShowOpenFileDialogAsync(string title, ReadOnlyMemory<FileDialogFilter> filters) =>
        ShowOpenFileDialogAsync(title, filters.ToArray());
}
