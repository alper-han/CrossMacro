
namespace CrossMacro.UI.Views.Dialogs;

public partial class FastLoopWarningDialog : Window
{
    public FastLoopWarningDialog()
    {
        InitializeComponent();
    }

    public FastLoopWarningDialog(
        string title,
        string message,
        string continueText,
        string cancelText,
        string suppressText) : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
        ContinueButton.Content = continueText;
        CancelButton.Content = cancelText;
        SuppressWarningCheckBox.Content = suppressText;
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(new FastLoopWarningResult(
            ContinuePlayback: true,
            SuppressFutureWarnings: SuppressWarningCheckBox.IsChecked is true));
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(FastLoopWarningResult.Cancelled);
    }
}
