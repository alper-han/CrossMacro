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
    
    private void OnCloseApp(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
