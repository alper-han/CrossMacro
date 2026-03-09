using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CrossMacro.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
