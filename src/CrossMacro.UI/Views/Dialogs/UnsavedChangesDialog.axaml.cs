namespace CrossMacro.UI.Views.Dialogs;

public partial class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog()
    {
        InitializeComponent();
    }

    public UnsavedChangesDialog(
        string title,
        string message,
        string saveText,
        string discardText,
        string cancelText) : this()
    {
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        SaveButton.Content = saveText;
        DiscardButton.Content = discardText;
        CancelButton.Content = cancelText;
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Save);

    private void DiscardButton_Click(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Discard);

    private void CancelButton_Click(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Cancel);
}
