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
/// ViewModel for the Schedule tab - manages scheduled macro tasks
/// </summary>
public partial class ScheduleViewModel : ViewModelBase, IDisposable
{
    private readonly ISchedulerService _schedulerService;
    private readonly IDialogService _dialogService;
    private readonly object _initializeLock = new();
    private Task? _initializeTask;
    private ScheduledTask? _selectedTask;
    private bool _isIntervalSelected = true;
    private bool _isDateTimeSelected;
    private bool _disposed;
    
    public ObservableCollection<ScheduledTask> Tasks => _schedulerService.Tasks;
    
    public ScheduledTask? SelectedTask
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
                UpdateScheduleTypeSelection();
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
                // Notify that SelectedTask changed so CanBeEnabled updates
                OnPropertyChanged(nameof(SelectedTask));
            }
        }
    }
    
    public string SelectedMacroFileName => 
        string.IsNullOrEmpty(SelectedTask?.MacroFilePath) 
            ? "No file selected" 
            : Path.GetFileName(SelectedTask.MacroFilePath);
    
    public bool IsIntervalSelected
    {
        get => _isIntervalSelected;
        set
        {
            if (_isIntervalSelected != value)
            {
                _isIntervalSelected = value;
                OnPropertyChanged();
                if (value && SelectedTask != null)
                {
                    SelectedTask.Type = ScheduleType.Interval;
                    _isDateTimeSelected = false;
                    OnPropertyChanged(nameof(IsDateTimeSelected));
                }
            }
        }
    }
    
    public bool IsDateTimeSelected
    {
        get => _isDateTimeSelected;
        set
        {
            if (_isDateTimeSelected != value)
            {
                _isDateTimeSelected = value;
                OnPropertyChanged();
                if (value && SelectedTask != null)
                {
                    SelectedTask.Type = ScheduleType.SpecificTime;
                    _isIntervalSelected = false;
                    OnPropertyChanged(nameof(IsIntervalSelected));
                }
            }
        }
    }
    
    // Events for global status
    public event EventHandler<string>? StatusChanged;
    
    public ScheduleViewModel(ISchedulerService schedulerService, IDialogService dialogService)
    {
        _schedulerService = schedulerService;
        _dialogService = dialogService;
        
        // Subscribe to task execution events
        _schedulerService.TaskStarting += OnTaskStarting;
        _schedulerService.TaskExecuted += OnTaskExecuted;
        
    }
    
    public Task InitializeAsync()
    {
        lock (_initializeLock)
        {
            _initializeTask ??= InitializeCoreAsync();
            return _initializeTask;
        }
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            await _schedulerService.LoadAsync();
            _schedulerService.Start();
        }
        catch (Exception ex)
        {
            RaiseStatus($"Schedule: failed to initialize ({ex.Message})");
        }
    }
    
    public DateTimeOffset? ScheduledDate
    {
        get => SelectedTask?.ScheduledDateTime == null ? null : new DateTimeOffset(SelectedTask.ScheduledDateTime.Value);
        set
        {
            if (SelectedTask != null && value.HasValue)
            {
                var current = SelectedTask.ScheduledDateTime ?? DateTime.Now;
                // Preserve time, change date
                var newDateTime = value.Value.Date + current.TimeOfDay;
                
                if (SelectedTask.ScheduledDateTime != newDateTime)
                {
                    SelectedTask.ScheduledDateTime = newDateTime;
                    if (SelectedTask.IsEnabled)
                    {
                        SelectedTask.CalculateNextRunTime();
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedTask)); // Update NextRunTime display
                }
            }
        }
    }

    public TimeSpan? ScheduledTime
    {
        get => SelectedTask?.ScheduledDateTime?.TimeOfDay;
        set
        {
            if (SelectedTask != null && value.HasValue)
            {
                var current = SelectedTask.ScheduledDateTime ?? DateTime.Now;
                // Preserve date, change time (including seconds)
                var newDateTime = current.Date + value.Value;
                
                if (SelectedTask.ScheduledDateTime != newDateTime)
                {
                    SelectedTask.ScheduledDateTime = newDateTime;
                    if (SelectedTask.IsEnabled)
                    {
                        SelectedTask.CalculateNextRunTime();
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedTask)); // Update NextRunTime display
                }
            }
        }
    }

    private void UpdateScheduleTypeSelection()
    {
        if (SelectedTask != null)
        {
            _isIntervalSelected = SelectedTask.Type == ScheduleType.Interval;
            _isDateTimeSelected = SelectedTask.Type == ScheduleType.SpecificTime;
            OnPropertyChanged(nameof(IsIntervalSelected));
            OnPropertyChanged(nameof(IsDateTimeSelected));
            OnPropertyChanged(nameof(ScheduledDate));
            OnPropertyChanged(nameof(ScheduledTime));
        }
    }
    
    [RelayCommand]
    private void AddTask()
    {
        var task = new ScheduledTask
        {
            Name = $"Task {Tasks.Count + 1}",
            Type = ScheduleType.Interval,
            IntervalValue = 30,
            IntervalUnit = IntervalUnit.Seconds
        };
        _schedulerService.AddTask(task);
        SelectedTask = task;
    }
    
    [RelayCommand]
    private async Task RemoveTaskAsync(ScheduledTask? task)
    {
        if (task == null) return;
        
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Delete Task", 
            $"Are you sure you want to delete the task '{task.Name}'?");
            
        if (!confirmed) return;
        
        _schedulerService.RemoveTask(task.Id);
        if (SelectedTask?.Id == task.Id)
        {
            SelectedTask = Tasks.FirstOrDefault();
        }
        await SaveChangesAsync(showSuccessStatus: false);
    }
    
    [RelayCommand]
    private void SelectTask(ScheduledTask? task)
    {
        if (task != null)
        {
            // Toggle: if already selected, deselect; otherwise select
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
            await _schedulerService.SaveAsync();
            if (showSuccessStatus)
            {
                RaiseStatus("Schedule: changes saved");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ScheduleViewModel] Failed to save scheduled tasks");
            var status = $"Schedule: failed to save changes ({ex.Message})";
            RaiseStatus(status);
            try
            {
                await _dialogService.ShowMessageAsync("Schedule Save Failed", status);
            }
            catch (Exception dialogEx)
            {
                Log.Warning(dialogEx, "[ScheduleViewModel] Failed to show save error dialog");
            }
        }
    }
    
    public void OnTaskEnabledChanged(ScheduledTask task)
    {
        if (task.IsEnabled &&
            !string.IsNullOrWhiteSpace(task.MacroFilePath) &&
            !task.MacroFilePath.EndsWith(".macro", StringComparison.OrdinalIgnoreCase))
        {
            RaiseStatus("Schedule: macro file extension is not .macro (still allowed)");
        }

        _schedulerService.SetTaskEnabled(task.Id, task.IsEnabled);
    }
    
    private void OnTaskStarting(object? sender, ScheduledTask task)
    {
        Dispatcher.UIThread.Post(() =>
        {
            RaiseStatus($"Schedule: Running {task.Name}...");
            
            // Refresh the selected task to update status display
            if (SelectedTask?.Id == task.Id)
            {
                OnPropertyChanged(nameof(SelectedTask));
            }
        });
    }
    
    private void OnTaskExecuted(object? sender, TaskExecutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Update global status
            var statusText = e.Success 
                ? $"Schedule: {e.Task.Name} completed" 
                : $"Schedule: {e.Task.Name} - {e.Message}";
            RaiseStatus(statusText);
            
            // Refresh the selected task to update LastRunTime display
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
        
        // Unsubscribe from events to prevent memory leaks
        _schedulerService.TaskStarting -= OnTaskStarting;
        _schedulerService.TaskExecuted -= OnTaskExecuted;
    }
}
