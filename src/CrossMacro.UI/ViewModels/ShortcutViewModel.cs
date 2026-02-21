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
using Serilog;

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
        _ = InitializeAsyncSafe();
    }
    
    private async Task InitializeAsyncSafe()
    {
        try
        {
            await _shortcutService.LoadAsync();
            _shortcutService.Start();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ShortcutViewModel] Failed to initialize shortcuts");
            RaiseStatus($"Shortcut: failed to initialize ({ex.Message})");
        }
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
        await SaveChangesAsync(showSuccessStatus: false);
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
            new FileDialogFilter { Name = "Macro Files", Extensions = new[] { "macro" } }
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
        await SaveChangesAsync(showSuccessStatus: true);
    }

    private async Task SaveChangesAsync(bool showSuccessStatus)
    {
        try
        {
            await _shortcutService.SaveAsync();
            if (showSuccessStatus)
            {
                RaiseStatus("Shortcut: changes saved");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ShortcutViewModel] Failed to save shortcut tasks");
            var status = $"Shortcut: failed to save changes ({ex.Message})";
            RaiseStatus(status);
            try
            {
                await _dialogService.ShowMessageAsync("Shortcut Save Failed", status);
            }
            catch (Exception dialogEx)
            {
                Log.Warning(dialogEx, "[ShortcutViewModel] Failed to show save error dialog");
            }
        }
    }
    
    public void OnHotkeyChanged(string newHotkey)
    {
        SelectedHotkeyString = newHotkey;
    }
    
    private void OnShortcutStarting(object? sender, ShortcutTask task)
    {
        Dispatcher.UIThread.Post(() =>
        {
            RaiseStatus($"Shortcut: Running {task.Name}...");
            
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
            RaiseStatus(statusText);
            
            if (SelectedTask?.Id == e.Task.Id)
            {
                OnPropertyChanged(nameof(SelectedTask));
            }
        });
    }

    private void RaiseStatus(string message)
    {
        if (Avalonia.Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            StatusChanged?.Invoke(this, message);
            return;
        }

        Dispatcher.UIThread.Post(() => StatusChanged?.Invoke(this, message));
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _shortcutService.ShortcutStarting -= OnShortcutStarting;
        _shortcutService.ShortcutExecuted -= OnShortcutExecuted;
    }
}
