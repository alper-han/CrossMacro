
namespace CrossMacro.UI.Services;

public class DialogService : IDialogService
{
    private readonly IDesktopLifetimeContext _desktopLifetimeContext;
    private readonly ILocalizationService _localizationService;

    public DialogService(IDesktopLifetimeContext desktopLifetimeContext, ILocalizationService localizationService)
    {
        _desktopLifetimeContext = desktopLifetimeContext;
        _localizationService = localizationService;
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message, string yesText = "Yes", string noText = "No")
    {
        var owner = _desktopLifetimeContext.MainWindow;

        if (owner is null)
        {
            return false;
        }

        var resolvedYesText = yesText is "Yes" ? _localizationService["Dialog_Yes"] : yesText;
        var resolvedNoText = noText is "No" ? _localizationService["Dialog_No"] : noText;
        var dialog = new ConfirmationDialog(title, message, resolvedYesText, resolvedNoText);
        return await dialog.ShowDialog<bool>(owner).ConfigureAwait(false);
    }

    public async Task ShowMessageAsync(string title, string message, string buttonText = "OK")
    {
        var owner = _desktopLifetimeContext.MainWindow;

        if (owner is null)
        {
            return;
        }

        var resolvedButtonText = buttonText is "OK" ? _localizationService["Dialog_Ok"] : buttonText;
        var dialog = new ConfirmationDialog(title, message, resolvedButtonText, noText: null);
        await dialog.ShowDialog<bool>(owner).ConfigureAwait(false);
    }

    public async Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, FileDialogFilter[] filters)
    {
        var mainWindow = _desktopLifetimeContext.MainWindow;

        if (mainWindow is null)
        {
            return null;
        }

        var fileTypeChoices = filters.Select(f => new Avalonia.Platform.Storage.FilePickerFileType(f.Name)
        {
            Patterns = FileDialogFilter.NormalizePatterns(f.Extensions),
        }).ToList();

        var options = new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            FileTypeChoices = fileTypeChoices,
        };

        var file = await mainWindow.StorageProvider.SaveFilePickerAsync(options).ConfigureAwait(false);
        return file?.Path.LocalPath;
    }

    public async Task<string?> ShowOpenFileDialogAsync(string title, FileDialogFilter[] filters)
    {
        var mainWindow = _desktopLifetimeContext.MainWindow;

        if (mainWindow is null)
        {
            return null;
        }

        var fileTypeFilters = filters.Select(f => new Avalonia.Platform.Storage.FilePickerFileType(f.Name)
        {
            Patterns = FileDialogFilter.NormalizePatterns(f.Extensions),
        }).ToList();

        var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypeFilters,
        };

        var files = await mainWindow.StorageProvider.OpenFilePickerAsync(options).ConfigureAwait(false);
        return files?.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
