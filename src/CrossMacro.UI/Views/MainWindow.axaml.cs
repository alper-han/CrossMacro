
using Avalonia.Layout;

namespace CrossMacro.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(TextBox.CopyingToClipboardEvent, OnTextBoxCopyingToClipboard, RoutingStrategies.Bubble);
        AddHandler(TextBox.CuttingToClipboardEvent, OnTextBoxCuttingToClipboard, RoutingStrategies.Bubble);
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(
            static state =>
            {
                var window = (MainWindow)state!;
                RefreshContentLayout(window.MainContentControl);
            },
            this,
            DispatcherPriority.Render);
    }

    internal static void RefreshContentLayout(Layoutable content)
    {
        ArgumentNullException.ThrowIfNull(content);
        content.InvalidateMeasure();
        content.InvalidateArrange();
        content.InvalidateVisual();
    }

    private void OnTextBoxCopyingToClipboard(object? sender, RoutedEventArgs e)
    {
        var textBox = e.Source as TextBox ?? sender as TextBox;
        if (textBox is null)
        {
            return;
        }

        e.Handled = true;
        _ = CopyTextToClipboardAsync(textBox);
    }

    private void OnTextBoxCuttingToClipboard(object? sender, RoutedEventArgs e)
    {
        var textBox = e.Source as TextBox ?? sender as TextBox;
        if (textBox is null)
        {
            return;
        }

        e.Handled = true;
        _ = CutTextToClipboardAsync(textBox);
    }

    private static async Task CopyTextToClipboardAsync(TextBox textBox)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(textBox)?.Clipboard;
            if (clipboard is null)
            {
                Log.Warning("[TextBoxClipboard] Clipboard is unavailable; copy skipped");
                return;
            }

            _ = await TextBoxClipboardHandler.TryCopyAsync(
                textBox,
                text => clipboard.SetTextAsync(text)).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[TextBoxClipboard] Unexpected copy failure; keeping the application alive");
        }
    }

    private static async Task CutTextToClipboardAsync(TextBox textBox)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(textBox)?.Clipboard;
            if (clipboard is null)
            {
                Log.Warning("[TextBoxClipboard] Clipboard is unavailable; cut skipped");
                return;
            }

            _ = await TextBoxClipboardHandler.TryCutAsync(
                textBox,
                text => clipboard.SetTextAsync(text)).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[TextBoxClipboard] Unexpected cut failure; keeping the application alive");
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnMinimizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseApp(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnDismissAppNotification(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.DismissAppNotification();
        }
    }
}
