using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Shortcuts tab - manages shortcut-triggered macro tasks
/// </summary>
public partial class ShortcutViewModel : ViewModelBase, IDisposable
{
    private readonly IShortcutService _shortcutService;
    private readonly IDialogService _dialogService;
    private ShortcutTask? _selectedTask;
    private bool _disposed;
    
    public ObservableCollection<ShortcutTask> Tasks => _shortcutService.Tasks;
    
    public ShortcutTask? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (_selectedTask != value)
            {
                _selectedTask = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedTask));
                OnPropertyChanged(nameof(SelectedMacroFilePath));
                OnPropertyChanged(nameof(SelectedMacroFileName));
                OnPropertyChanged(nameof(SelectedHotkeyString));
            }
        }
    }
    
    public bool HasSelectedTask => SelectedTask != null;
    
    public string? SelectedMacroFilePath
    {
        get => string.IsNullOrEmpty(SelectedTask?.MacroFilePath) ? null : SelectedTask.MacroFilePath;
        set
        {
            if (SelectedTask != null && SelectedTask.MacroFilePath != (value ?? ""))
            {
                SelectedTask.MacroFilePath = value ?? "";
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedMacroFileName));
                OnPropertyChanged(nameof(SelectedTask));
            }
        }
    }
    
    public string SelectedMacroFileName => 
        string.IsNullOrEmpty(SelectedTask?.MacroFilePath) 
            ? "No file selected" 
            : Path.GetFileName(SelectedTask.MacroFilePath);
    
    public string SelectedHotkeyString
    {
        get => SelectedTask?.HotkeyString ?? "";
        set
        {
            if (SelectedTask != null && SelectedTask.HotkeyString != value)
            {
                SelectedTask.HotkeyString = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedTask));
            }
        }
    }
    
    // Events for global status
    public event EventHandler<string>? StatusChanged;
    
    public ShortcutViewModel(IShortcutService shortcutService, IDialogService dialogService)
    {
        _shortcutService = shortcutService;
        _dialogService = dialogService;
        
        // Subscribe to shortcut execution events
        _shortcutService.ShortcutStarting += OnShortcutStarting;
        _shortcutService.ShortcutExecuted += OnShortcutExecuted;
        
        // Load saved shortcuts and start listening
        _ = InitializeAsync();
    }
    
    private async Task InitializeAsync()
    {
        await _shortcutService.LoadAsync();
        _shortcutService.Start();
    }
    
    [RelayCommand]
    private void AddTask()
    {
        var task = new ShortcutTask
        {
            Name = $"Shortcut {Tasks.Count + 1}"
        };
        _shortcutService.AddTask(task);
        SelectedTask = task;
    }
    
    [RelayCommand]
    private async Task RemoveTaskAsync(ShortcutTask? task)
    {
        if (task == null) return;
        
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Delete Shortcut", 
            $"Are you sure you want to delete the shortcut '{task.Name}'?");
            
        if (!confirmed) return;
        
        _shortcutService.RemoveTask(task.Id);
        if (SelectedTask?.Id == task.Id)
        {
            SelectedTask = Tasks.FirstOrDefault();
        }
        _ = _shortcutService.SaveAsync();
    }
    
    [RelayCommand]
    private void SelectTask(ShortcutTask? task)
    {
        if (task != null)
        {
            SelectedTask = SelectedTask?.Id == task.Id ? null : task;
        }
    }
    
    [RelayCommand]
    private async Task BrowseMacroAsync()
    {
        if (SelectedTask == null) return;
        
        var filters = new FileDialogFilter[]
        {
            new FileDialogFilter { Name = "Macro Files", Extensions = new[] { "*.macro" } }
        };
        
        var filePath = await _dialogService.ShowOpenFileDialogAsync(
            "Select Macro File",
            filters);
        
        if (!string.IsNullOrEmpty(filePath))
        {
            SelectedMacroFilePath = filePath;
        }
    }
    
    [RelayCommand]
    private async Task SaveAsync()
    {
        await _shortcutService.SaveAsync();
    }
    
    public void OnHotkeyChanged(string newHotkey)
    {
        SelectedHotkeyString = newHotkey;
    }
    
    private void OnShortcutStarting(object? sender, ShortcutTask task)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusChanged?.Invoke(this, $"Shortcut: Running {task.Name}...");
            
            if (SelectedTask?.Id == task.Id)
            {
                OnPropertyChanged(nameof(SelectedTask));
            }
        });
    }
    
    private void OnShortcutExecuted(object? sender, ShortcutExecutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var statusText = e.Success 
                ? $"Shortcut: {e.Task.Name} completed" 
                : $"Shortcut: {e.Task.Name} - {e.Message}";
            StatusChanged?.Invoke(this, statusText);
            
            if (SelectedTask?.Id == e.Task.Id)
            {
                OnPropertyChanged(nameof(SelectedTask));
            }
        });
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _shortcutService.ShortcutStarting -= OnShortcutStarting;
        _shortcutService.ShortcutExecuted -= OnShortcutExecuted;
    }
}
