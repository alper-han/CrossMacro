using Avalonia.Controls;
using Avalonia.Interactivity;
using CrossMacro.UI.ViewModels;

namespace CrossMacro.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnStartRecording(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.StartRecording();
        }
    }

    private void OnStopRecording(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.StopRecording();
        }
    }

    private void OnPlayMacro(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PlayMacro();
        }
    }

    private void OnSaveMacro(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.SaveMacro();
        }
    }

    private void OnLoadMacro(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.LoadMacro();
        }
    }

    private void OnCloseApp(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
