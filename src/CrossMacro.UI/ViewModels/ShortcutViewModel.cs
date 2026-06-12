using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Shortcuts tab - manages shortcut-triggered macro tasks
/// </summary>
public partial class ShortcutViewModel : ViewModelBase, IDisposable
{
    private readonly IShortcutService _shortcutService;
    private readonly IDialogService _dialogService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ILocalizationService _localizationService;
    private ShortcutTask? _selectedTask;
    private bool _disposed;
    
    public ObservableCollection<ShortcutTask> Tasks => _shortcutService.Tasks;

    public IGlobalHotkeyService GlobalHotkeyService => _hotkeyService;

    public ILocalizationService LocalizationService => _localizationService;

    public Task InitializationTask { get; }

    public string TaskCountText => string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_ItemsText"], Tasks.Count);
    
    public ShortcutTask? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (_selectedTask != value)
            {
                if (_selectedTask != null)
                {
                    _selectedTask.PropertyChanged -= OnSelectedTaskPropertyChanged;
                }

                _selectedTask = value;
                if (_selectedTask != null)
                {
                    _selectedTask.PropertyChanged += OnSelectedTaskPropertyChanged;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedTask));
                OnPropertyChanged(nameof(SelectedMacroFilePath));
                OnPropertyChanged(nameof(SelectedMacroFileName));
                OnPropertyChanged(nameof(SelectedHotkeyString));
                OnSelectedTaskStatusChanged();
            }
        }
    }

    private void OnSelectedTaskPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShortcutTask.LastTriggeredTime) or nameof(ShortcutTask.LastStatus))
        {
            RaiseOnUiThread(OnSelectedTaskStatusChanged);
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

    public string SelectedLastTriggeredText => SelectedTask?.LastTriggeredTime?.ToLocalTime().ToString("G", _localizationService.CurrentCulture)
        ?? _localizationService["Shortcut_Never"];

    public string SelectedStatusText => string.IsNullOrWhiteSpace(SelectedTask?.LastStatus)
        ? _localizationService["Shortcut_StatusPlaceholder"]
        : SelectedTask.LastStatus!;
    
    // Events for global status
    public event EventHandler<string>? StatusChanged;
    
    public ShortcutViewModel(
        IShortcutService shortcutService,
        IDialogService dialogService,
        IGlobalHotkeyService hotkeyService,
        ILocalizationService localizationService)
    {
        _shortcutService = shortcutService;
        _dialogService = dialogService;
        _hotkeyService = hotkeyService;
        _localizationService = localizationService;
        _localizationService.CultureChanged += OnCultureChanged;
        
        // Subscribe to shortcut execution events
        _shortcutService.ShortcutStarting += OnShortcutStarting;
        _shortcutService.ShortcutExecuted += OnShortcutExecuted;
        _shortcutService.Tasks?.CollectionChanged += OnTasksCollectionChanged;
        
        // Load saved shortcuts and start listening
        InitializationTask = InitializeAsyncSafe();
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

    public void RefreshProfileData()
    {
        SelectedTask = Tasks.FirstOrDefault();
        OnPropertyChanged(nameof(Tasks));
        OnPropertyChanged(nameof(TaskCountText));
        OnPropertyChanged(nameof(SelectedTask));
        OnPropertyChanged(nameof(SelectedMacroFileName));
        OnPropertyChanged(nameof(SelectedHotkeyString));
        OnSelectedTaskStatusChanged();
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

    [RelayCommand]
    private async Task TaskEnabledChangedAsync(ShortcutTask task)
    {
        _shortcutService.SetTaskEnabled(task.Id, task.IsEnabled);
        await SaveChangesAsync(showSuccessStatus: false);
    }
    
    private void OnShortcutStarting(object? sender, ShortcutTask task)
    {
        Dispatcher.UIThread.Post(() =>
        {
            RaiseStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_StatusRunning"], task.Name));
            
            if (SelectedTask?.Id == task.Id)
            {
                OnPropertyChanged(nameof(SelectedTask));
                OnSelectedTaskStatusChanged();
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
                OnSelectedTaskStatusChanged();
            }
        });
    }

    private void OnSelectedTaskStatusChanged()
    {
        OnPropertyChanged(nameof(SelectedLastTriggeredText));
        OnPropertyChanged(nameof(SelectedStatusText));
    }

    private void OnTasksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(TaskCountText));
    }

    private static void RaiseOnUiThread(Action action)
    {
        if (Avalonia.Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
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
        OnSelectedTaskStatusChanged();
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _shortcutService.ShortcutStarting -= OnShortcutStarting;
        _shortcutService.ShortcutExecuted -= OnShortcutExecuted;
        _shortcutService.Tasks?.CollectionChanged -= OnTasksCollectionChanged;
        if (_selectedTask != null)
        {
            _selectedTask.PropertyChanged -= OnSelectedTaskPropertyChanged;
        }
        _localizationService.CultureChanged -= OnCultureChanged;
    }
}
