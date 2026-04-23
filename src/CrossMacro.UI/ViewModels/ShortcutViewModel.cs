using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;
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
    private readonly ILocalizationService _localizationService;
    private ShortcutTask? _selectedTask;
    private bool _disposed;
    
    public ObservableCollection<ShortcutTask> Tasks => _shortcutService.Tasks;

    public string TaskCountText => string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_ItemsText"], Tasks.Count);
    
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
            ? _localizationService["Shortcut_NoFileSelected"] 
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
    
    public ShortcutViewModel(IShortcutService shortcutService, IDialogService dialogService, ILocalizationService localizationService)
    {
        _shortcutService = shortcutService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _localizationService.CultureChanged += OnCultureChanged;
        
        // Subscribe to shortcut execution events
        _shortcutService.ShortcutStarting += OnShortcutStarting;
        _shortcutService.ShortcutExecuted += OnShortcutExecuted;
        _shortcutService.Tasks?.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TaskCountText));
        
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
            RaiseStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_StatusInitFailed"], ex.Message));
        }
    }
    
    [RelayCommand]
    private void AddTask()
    {
        var task = new ShortcutTask
        {
            Name = string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_DefaultTaskName"], Tasks.Count + 1)
        };
        _shortcutService.AddTask(task);
        SelectedTask = task;
        OnPropertyChanged(nameof(TaskCountText));
    }
    
    [RelayCommand]
    private async Task RemoveTaskAsync(ShortcutTask? task)
    {
        if (task == null) return;
        
        var confirmed = await _dialogService.ShowConfirmationAsync(
            _localizationService["Shortcut_DeleteTitle"],
            string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_DeleteMessage"], task.Name));
            
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
            new FileDialogFilter { Name = _localizationService["Shortcut_OpenMacroDialogFilter"], Extensions = new[] { "macro" } }
        };
        
        var filePath = await _dialogService.ShowOpenFileDialogAsync(
            _localizationService["Shortcut_OpenMacroDialogTitle"],
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
                RaiseStatus(_localizationService["Shortcut_StatusChangesSaved"]);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ShortcutViewModel] Failed to save shortcut tasks");
            var status = string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_StatusSaveFailed"], ex.Message);
            RaiseStatus(status);
            try
            {
                await _dialogService.ShowMessageAsync(_localizationService["Shortcut_SaveFailedTitle"], status);
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
            RaiseStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_StatusRunning"], task.Name));
            
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
                ? string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_StatusCompleted"], e.Task.Name)
                : string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_StatusFailed"], e.Task.Name, e.Message);
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

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(TaskCountText));
        OnPropertyChanged(nameof(SelectedMacroFileName));
        OnPropertyChanged(nameof(SelectedTask));
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _shortcutService.ShortcutStarting -= OnShortcutStarting;
        _shortcutService.ShortcutExecuted -= OnShortcutExecuted;
        _localizationService.CultureChanged -= OnCultureChanged;
    }
}
