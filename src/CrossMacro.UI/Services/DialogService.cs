using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CrossMacro.UI.Views.Dialogs;

namespace CrossMacro.UI.Services;

public class DialogService : IDialogService
{
    public async Task<bool> ShowConfirmationAsync(string title, string message, string yesText = "Yes", string noText = "No")
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow;

        if (owner == null)
        {
            return false;
        }

        var dialog = new ConfirmationDialog(title, message, yesText, noText);
        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task ShowMessageAsync(string title, string message, string buttonText = "OK")
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow;

        if (owner == null)
        {
            return;
        }

        var dialog = new ConfirmationDialog(title, message, buttonText, null);
        await dialog.ShowDialog<bool>(owner);
    }

    public async Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, FileDialogFilter[] filters)
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var mainWindow = desktop?.MainWindow;

        if (mainWindow == null) return null;

        var fileTypeChoices = filters.Select(f => new Avalonia.Platform.Storage.FilePickerFileType(f.Name)
        {
            Patterns = FileDialogFilter.NormalizePatterns(f.Extensions)
        }).ToList();

        var options = new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            FileTypeChoices = fileTypeChoices
        };

        var file = await mainWindow.StorageProvider.SaveFilePickerAsync(options);
        return file?.Path.LocalPath;
    }

    public async Task<string?> ShowOpenFileDialogAsync(string title, FileDialogFilter[] filters)
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var mainWindow = desktop?.MainWindow;

        if (mainWindow == null) return null;

        var fileTypeFilters = filters.Select(f => new Avalonia.Platform.Storage.FilePickerFileType(f.Name)
        {
            Patterns = FileDialogFilter.NormalizePatterns(f.Extensions)
        }).ToList();

        var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypeFilters
        };

        var files = await mainWindow.StorageProvider.OpenFilePickerAsync(options);
        return files?.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
