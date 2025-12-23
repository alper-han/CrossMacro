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
        var dialog = new ConfirmationDialog(title, message, yesText, noText);
        
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow; // Or try to find the active window if needed

        if (owner != null)
        {
            var result = await dialog.ShowDialog<bool>(owner);
            return result;
        }
        
        return false;
    }
    public async Task ShowMessageAsync(string title, string message, string buttonText = "OK")
    {
        var dialog = new ConfirmationDialog(title, message, buttonText, null); 
        
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow; // Or try to find the active window if needed

        if (owner != null)
        {
            await dialog.ShowDialog<bool>(owner);
        }
    }
}
