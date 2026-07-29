
namespace CrossMacro.UI.Views.Dialogs;

public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog()
    {
        InitializeComponent();
    }

    public ConfirmationDialog(
        string title,
        string message,
        string yesText,
        string? noText,
        bool dangerYes = true,
        bool dangerNo = false) : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
        YesButton.Content = yesText;
        NoButton.Content = noText;

        SetButtonStyle(YesButton, dangerYes ? "danger" : "primary");
        SetButtonStyle(NoButton, dangerNo ? "danger" : "secondary");

        if (string.IsNullOrEmpty(noText))
        {
            NoButton.IsVisible = false;
        }
    }

    private void YesButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(dialogResult: true);
    }

    private void NoButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(dialogResult: false);
    }

    private static void SetButtonStyle(Button button, string styleClass)
    {
        _ = button.Classes.Remove("primary");
        _ = button.Classes.Remove("secondary");
        _ = button.Classes.Remove("danger");
        button.Classes.Add(styleClass);
    }
}
