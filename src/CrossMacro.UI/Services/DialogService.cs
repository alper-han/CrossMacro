
namespace CrossMacro.UI.Services;

public class DialogService(IDesktopLifetimeContext desktopLifetimeContext, ILocalizationService localizationService) : IDialogService
{
    private readonly IDesktopLifetimeContext _desktopLifetimeContext = desktopLifetimeContext;
    private readonly ILocalizationService _localizationService = localizationService;

    public async Task<bool> ShowConfirmationAsync(string title, string message, string yesText = "Yes", string noText = "No")
    {
        var owner = _desktopLifetimeContext.MainWindow;
        if (owner is null)
        {
            return false;
        }

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            return await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => ShowConfirmationAsync(title, message, yesText, noText)).ConfigureAwait(false);
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

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => ShowMessageAsync(title, message, buttonText)).ConfigureAwait(false);
            return;
        }

        var resolvedButtonText = buttonText is "OK" ? _localizationService["Dialog_Ok"] : buttonText;
        var dialog = new ConfirmationDialog(title, message, resolvedButtonText, noText: null);
        _ = await dialog.ShowDialog<bool>(owner).ConfigureAwait(false);
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

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            return await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(
                () => ShowFastLoopWarningAsync(title, message, continueText, cancelText, suppressText)).ConfigureAwait(false);
        }

        var dialog = new FastLoopWarningDialog(title, message, continueText, cancelText, suppressText);
        return await dialog.ShowDialog<FastLoopWarningResult>(owner).ConfigureAwait(false);
    }

    public async Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, FileDialogFilter[] filters)
    {
        var mainWindow = _desktopLifetimeContext.MainWindow;
        if (mainWindow is null)
        {
            return null;
        }

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            return await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => ShowSaveFileDialogAsync(title, defaultFileName, filters)).ConfigureAwait(false);
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

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            return await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => ShowOpenFileDialogAsync(title, filters)).ConfigureAwait(false);
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
