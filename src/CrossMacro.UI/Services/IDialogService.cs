using System.Threading.Tasks;

namespace CrossMacro.UI.Services;

public interface IDialogService
{
    Task<bool> ShowConfirmationAsync(string title, string message, string yesText = "Yes", string noText = "No");
    Task ShowMessageAsync(string title, string message, string buttonText = "OK");
}
