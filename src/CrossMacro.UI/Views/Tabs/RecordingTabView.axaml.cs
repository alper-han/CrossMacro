using Avalonia.Controls;
using Avalonia.Interactivity;
using CrossMacro.UI.ViewModels;

namespace CrossMacro.UI.Views.Tabs;

public partial class RecordingTabView : UserControl
{
    public RecordingTabView()
    {
        InitializeComponent();
    }
    
    private void OnStartRecording(object? sender, RoutedEventArgs e)
    {
        if (DataContext is RecordingViewModel vm)
        {
            _ = vm.StartRecordingAsync();
        }
    }
    
    private void OnStopRecording(object? sender, RoutedEventArgs e)
    {
        if (DataContext is RecordingViewModel vm)
        {
            vm.StopRecording();
        }
    }
}
