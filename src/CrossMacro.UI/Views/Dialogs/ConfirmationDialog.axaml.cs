using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CrossMacro.UI.Views.Dialogs;

public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog()
    {
        InitializeComponent();
    }

    public ConfirmationDialog(string title, string message, string yesText, string? noText) : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
        YesButton.Content = yesText;
        NoButton.Content = noText;
        
        if (string.IsNullOrEmpty(noText))
        {
            NoButton.IsVisible = false;
        }
    }

    private void YesButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void NoButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
