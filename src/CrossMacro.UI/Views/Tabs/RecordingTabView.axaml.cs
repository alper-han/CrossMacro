using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CrossMacro.UI.ViewModels;

namespace CrossMacro.UI.Views.Tabs;

public partial class RecordingTabView : UserControl
{
    private RecordingViewModel? _currentVm;
    
    public RecordingTabView()
    {
        InitializeComponent();
        
        // Subscribe to IsRecording changes to update button style
        DataContextChanged += OnDataContextChanged;
    }
    
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from old ViewModel to prevent memory leaks
        if (_currentVm != null)
        {
            _currentVm.PropertyChanged -= OnViewModelPropertyChanged;
            _currentVm = null;
        }
        
        // Subscribe to new ViewModel
        if (DataContext is RecordingViewModel vm)
        {
            _currentVm = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            // Initialize button state
            UpdateButtonState(vm.IsRecording);
        }
    }
    
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(RecordingViewModel.IsRecording) && _currentVm != null)
        {
            UpdateButtonState(_currentVm.IsRecording);
        }
    }
    
    private void UpdateButtonState(bool isRecording)
    {
        var button = this.FindControl<Button>("RecordingToggleButton");
        if (button != null)
        {
            if (isRecording)
                button.Classes.Add("recording");
            else
                button.Classes.Remove("recording");
        }
    }
    
    private void OnToggleRecording(object? sender, RoutedEventArgs e)
    {
        if (DataContext is RecordingViewModel vm)
        {
            vm.ToggleRecording();
        }
    }
}
