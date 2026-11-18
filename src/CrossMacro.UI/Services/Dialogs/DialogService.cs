using CrossMacro.UI.Views.Dialogs;

namespace CrossMacro.UI.Services.Dialogs;

public class DialogService(
    IDesktopLifetimeContext desktopLifetimeContext,
    ILocalizationService localizationService,
    IUiDispatcher uiDispatcher) : IDialogService
{
    private readonly IDesktopLifetimeContext _desktopLifetimeContext = desktopLifetimeContext;
    private readonly ILocalizationService _localizationService = localizationService;
    private readonly IUiDispatcher _uiDispatcher = uiDispatcher;

    public async Task<bool> ShowConfirmationAsync(string title, string message, string yesText = "Yes", string noText = "No")
    {
        var owner = _desktopLifetimeContext.MainWindow;
        if (owner is null)
        {
            return false;
        }

        var resolvedYesText = yesText is "Yes" ? _localizationService["Dialog_Yes"] : yesText;
        var resolvedNoText = noText is "No" ? _localizationService["Dialog_No"] : noText;
        return await RunOnUiThreadAsync(() =>
            new ConfirmationDialog(title, message, resolvedYesText, resolvedNoText).ShowDialog<bool>(owner)).ConfigureAwait(false);
    }

    public async Task ShowMessageAsync(string title, string message, string buttonText = "OK")
    {
        var owner = _desktopLifetimeContext.MainWindow;
        if (owner is null)
        {
            return;
        }

        var resolvedButtonText = buttonText is "OK" ? _localizationService["Dialog_Ok"] : buttonText;
        _ = await RunOnUiThreadAsync(() =>
            new ConfirmationDialog(title, message, resolvedButtonText, noText: null).ShowDialog<bool>(owner)).ConfigureAwait(false);
    }

    public async Task<UnsavedChangesChoice> ShowUnsavedChangesAsync(
        string title,
        string message,
        string saveText,
        string discardText,
        string cancelText)
    {
        var owner = _desktopLifetimeContext.MainWindow;
        if (owner is null)
        {
            return UnsavedChangesChoice.Cancel;
        }

        return await RunOnUiThreadAsync(() =>
            new UnsavedChangesDialog(title, message, saveText, discardText, cancelText).ShowDialog<UnsavedChangesChoice>(owner)).ConfigureAwait(false);
    }

    public async Task<FastLoopWarningResult> ShowFastLoopWarningAsync(
        string title,
        string message,
        string continueText,
        string cancelText,
        string suppressText)
    {
        var owner = _desktopLifetimeContext.MainWindow;
        if (owner is null)
        {
            return FastLoopWarningResult.Cancelled;
        }

        return await RunOnUiThreadAsync(() =>
            new FastLoopWarningDialog(title, message, continueText, cancelText, suppressText).ShowDialog<FastLoopWarningResult>(owner)).ConfigureAwait(false);
    }

    public async Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, IReadOnlyList<FileDialogFilter> filters)
    {
        var mainWindow = _desktopLifetimeContext.MainWindow;
        if (mainWindow is null)
        {
            return null;
        }

        return await RunOnUiThreadAsync(async () =>
        {
            var options = new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = defaultFileName,
                FileTypeChoices = ToFilePickerFileTypes(filters),
            };

            var file = await mainWindow.StorageProvider.SaveFilePickerAsync(options).ConfigureAwait(false);
            return file?.Path.LocalPath;
        }).ConfigureAwait(false);
    }

    public async Task<string?> ShowOpenFileDialogAsync(string title, IReadOnlyList<FileDialogFilter> filters)
    {
        var mainWindow = _desktopLifetimeContext.MainWindow;
        if (mainWindow is null)
        {
            return null;
        }

        return await RunOnUiThreadAsync(async () =>
        {
            var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = ToFilePickerFileTypes(filters),
            };

            var files = await mainWindow.StorageProvider.OpenFilePickerAsync(options).ConfigureAwait(false);
            return files.Count > 0 ? files[0].Path.LocalPath : null;
        }).ConfigureAwait(false);
    }

    private static List<Avalonia.Platform.Storage.FilePickerFileType> ToFilePickerFileTypes(IReadOnlyList<FileDialogFilter> filters) =>
        [.. filters.Select(f => new Avalonia.Platform.Storage.FilePickerFileType(f.Name)
        {
            Patterns = FileDialogFilter.NormalizePatterns(f.Extensions),
        })];

    private async Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> showDialog) =>
        await (await _uiDispatcher.InvokeAsync(showDialog).ConfigureAwait(false)).ConfigureAwait(false);
}
