using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
/// ViewModel for the Schedule tab - manages scheduled macro tasks
/// </summary>
public partial class ScheduleViewModel : ViewModelBase, IDisposable
{
    private readonly ISchedulerService _schedulerService;
    private readonly IDialogService _dialogService;
    private readonly ITimeProvider _timeProvider;
    private readonly ILocalizationService _localizationService;
    private readonly object _initializeLock = new();
    private Task? _initializeTask;
    private ScheduledTask? _selectedTask;
    private bool _isIntervalSelected = true;
    private bool _isDateTimeSelected;
    private bool _isWeeklySelected;
    private bool _useCustomWeeklyDays;
    private bool _disposed;

    public ObservableCollection<ScheduledTask> Tasks => _schedulerService.Tasks ?? new ObservableCollection<ScheduledTask>();

    public IReadOnlyList<IntervalUnitOption> IntervalUnitOptions => new[]
    {
        new IntervalUnitOption(IntervalUnit.Seconds, _localizationService["Schedule_Seconds"]),
        new IntervalUnitOption(IntervalUnit.Minutes, _localizationService["Schedule_Minutes"]),
        new IntervalUnitOption(IntervalUnit.Hours, _localizationService["Schedule_Hours"])
    };

    public IReadOnlyList<WeeklyPresetOption> WeeklyPresetOptions => new[]
    {
        new WeeklyPresetOption(ScheduleDays.EveryDay, _localizationService["Schedule_WeeklyEveryDay"]),
        new WeeklyPresetOption(ScheduleDays.Weekdays, _localizationService["Schedule_WeeklyWeekdays"]),
        new WeeklyPresetOption(ScheduleDays.Weekends, _localizationService["Schedule_WeeklyWeekends"]),
        new WeeklyPresetOption(null, _localizationService["Schedule_WeeklyCustom"])
    };

    public IReadOnlyList<WeeklyDayOption> WeeklyDayOptions => new WeeklyDayOption[]
    {
        new(this, ScheduleDays.Monday, _localizationService["Schedule_Monday"]),
        new(this, ScheduleDays.Tuesday, _localizationService["Schedule_Tuesday"]),
        new(this, ScheduleDays.Wednesday, _localizationService["Schedule_Wednesday"]),
        new(this, ScheduleDays.Thursday, _localizationService["Schedule_Thursday"]),
        new(this, ScheduleDays.Friday, _localizationService["Schedule_Friday"]),
        new(this, ScheduleDays.Saturday, _localizationService["Schedule_Saturday"]),
        new(this, ScheduleDays.Sunday, _localizationService["Schedule_Sunday"])
    };

    public string TaskCountText => string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_ItemsText"], Tasks.Count);

    public IntervalUnitOption? SelectedIntervalUnit
    {
        get => IntervalUnitOptions.FirstOrDefault(option => option.Value == SelectedTask?.IntervalUnit);
        set
        {
            if (SelectedTask != null && value != null && SelectedTask.IntervalUnit != value.Value)
            {
                SelectedTask.IntervalUnit = value.Value;
                OnPropertyChanged();
            }
        }
    }
    
    public ScheduledTask? SelectedTask
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
                OnPropertyChanged(nameof(SelectedIntervalUnit));
                UpdateScheduleTypeSelection();
                OnSelectedTaskStatusChanged();
            }
        }
    }

    private void OnSelectedTaskPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ScheduledTask.LastRunTime) or nameof(ScheduledTask.NextRunTime) or nameof(ScheduledTask.LastStatus))
        {
            RaiseOnUiThread(OnSelectedTaskStatusChanged);
            return;
        }

        if (sender is ScheduledTask task
            && task.IsEnabled
            && e.PropertyName is nameof(ScheduledTask.IntervalValue)
                or nameof(ScheduledTask.IntervalUnit)
                or nameof(ScheduledTask.UseRandomIntervalDelay)
                or nameof(ScheduledTask.IntervalMinValue)
                or nameof(ScheduledTask.IntervalMaxValue)
                or nameof(ScheduledTask.ScheduledDateTime)
                or nameof(ScheduledTask.WeeklyDays)
                or nameof(ScheduledTask.WeeklyTime)
                or nameof(ScheduledTask.Type))
        {
            RaiseOnUiThread(() =>
            {
                if (e.PropertyName == nameof(ScheduledTask.IntervalUnit))
                {
                    OnPropertyChanged(nameof(SelectedIntervalUnit));
                }

                if (e.PropertyName == nameof(ScheduledTask.WeeklyDays))
                {
                    SyncWeeklyPresetFromSelectedTask();
                    OnWeeklyDaySelectionChanged();
                    OnPropertyChanged(nameof(SelectedTask));
                }

                task.CalculateNextRunTime(_timeProvider.UtcNow);
                OnSelectedTaskStatusChanged();
            });
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
            ? _localizationService["Schedule_NoFileSelected"] 
            : Path.GetFileName(SelectedTask.MacroFilePath);

    public string SelectedLastRunText => SelectedTask?.LastRunTime?.ToLocalTime().ToString("G", _localizationService.CurrentCulture)
        ?? _localizationService["Schedule_Never"];

    public string SelectedNextRunText => SelectedTask?.NextRunTime?.ToLocalTime().ToString("G", _localizationService.CurrentCulture)
        ?? _localizationService["Schedule_NotScheduled"];

    public string SelectedStatusText => string.IsNullOrWhiteSpace(SelectedTask?.LastStatus)
        ? _localizationService["Schedule_StatusPlaceholder"]
        : SelectedTask.LastStatus!;

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
                    _isWeeklySelected = false;
                    OnPropertyChanged(nameof(IsDateTimeSelected));
                    OnPropertyChanged(nameof(IsWeeklySelected));
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
                    _isWeeklySelected = false;
                    OnPropertyChanged(nameof(IsIntervalSelected));
                    OnPropertyChanged(nameof(IsWeeklySelected));
                }
            }
        }
    }

    public bool IsWeeklySelected
    {
        get => _isWeeklySelected;
        set
        {
            if (_isWeeklySelected != value)
            {
                _isWeeklySelected = value;
                OnPropertyChanged();
                if (value && SelectedTask != null)
                {
                    SelectedTask.Type = ScheduleType.Weekly;
                    _isIntervalSelected = false;
                    _isDateTimeSelected = false;
                    OnPropertyChanged(nameof(IsIntervalSelected));
                    OnPropertyChanged(nameof(IsDateTimeSelected));
                }
            }
        }
    }
    
    // Events for global status
    public event EventHandler<string>? StatusChanged;
    
    public ScheduleViewModel(
        ISchedulerService schedulerService,
        IDialogService dialogService,
        ITimeProvider timeProvider,
        ILocalizationService localizationService)
    {
        _schedulerService = schedulerService ?? throw new ArgumentNullException(nameof(schedulerService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _localizationService = localizationService;
        _localizationService.CultureChanged += OnCultureChanged;
        
        // Subscribe to task execution events
        _schedulerService.TaskStarting += OnTaskStarting;
        _schedulerService.TaskExecuted += OnTaskExecuted;
        _schedulerService.Tasks?.CollectionChanged += OnTasksCollectionChanged;
        
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
            RaiseStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_StatusInitFailed"], ex.Message));
        }
    }
    
    public DateTimeOffset? ScheduledDate
    {
        get => SelectedTask?.ScheduledDateTime == null ? null : new DateTimeOffset(SelectedTask.ScheduledDateTime.Value);
        set
        {
            if (SelectedTask != null && value.HasValue)
            {
                var current = SelectedTask.ScheduledDateTime ?? _timeProvider.Now;
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
                var current = SelectedTask.ScheduledDateTime ?? _timeProvider.Now;
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

    public TimeSpan? WeeklyTime
    {
        get => SelectedTask?.WeeklyTime;
        set
        {
            if (SelectedTask != null && value.HasValue && SelectedTask.WeeklyTime != value.Value)
            {
                SelectedTask.WeeklyTime = value.Value;
                if (SelectedTask.IsEnabled)
                {
                    SelectedTask.CalculateNextRunTime(_timeProvider.UtcNow);
                }

                OnPropertyChanged();
                OnSelectedTaskStatusChanged();
            }
        }
    }

    public WeeklyPresetOption? SelectedWeeklyPreset
    {
        get
        {
            if (SelectedTask == null)
            {
                return null;
            }

            if (_useCustomWeeklyDays)
            {
                return WeeklyPresetOptions.FirstOrDefault(option => option.Value == null);
            }

            return WeeklyPresetOptions.FirstOrDefault(option => option.Value == SelectedTask.WeeklyDays)
                ?? WeeklyPresetOptions.FirstOrDefault(option => option.Value == null);
        }
        set
        {
            if (SelectedTask == null || value == null)
            {
                return;
            }

            if (value.Value == null)
            {
                _useCustomWeeklyDays = true;
                OnPropertyChanged();
                OnWeeklyDaySelectionChanged();
                return;
            }

            _useCustomWeeklyDays = false;
            if (SelectedTask.WeeklyDays != value.Value.Value)
            {
                SelectedTask.WeeklyDays = value.Value.Value;
            }

            OnPropertyChanged();
            OnWeeklyDaySelectionChanged();
        }
    }

    public bool IsWeeklyCustomSelected => _useCustomWeeklyDays;

    private void UpdateScheduleTypeSelection()
    {
        if (SelectedTask != null)
        {
            _isIntervalSelected = SelectedTask.Type == ScheduleType.Interval;
            _isDateTimeSelected = SelectedTask.Type == ScheduleType.SpecificTime;
            _isWeeklySelected = SelectedTask.Type == ScheduleType.Weekly;
            SyncWeeklyPresetFromSelectedTask();
            OnPropertyChanged(nameof(IsIntervalSelected));
            OnPropertyChanged(nameof(IsDateTimeSelected));
            OnPropertyChanged(nameof(IsWeeklySelected));
            OnPropertyChanged(nameof(ScheduledDate));
            OnPropertyChanged(nameof(ScheduledTime));
            OnPropertyChanged(nameof(WeeklyTime));
            OnPropertyChanged(nameof(SelectedWeeklyPreset));
            OnWeeklyDaySelectionChanged();
        }
    }

    private void SyncWeeklyPresetFromSelectedTask()
    {
        _useCustomWeeklyDays = SelectedTask?.WeeklyDays is not (ScheduleDays.EveryDay or ScheduleDays.Weekdays or ScheduleDays.Weekends);
    }

    internal bool HasWeeklyDay(ScheduleDays day)
    {
        return SelectedTask?.WeeklyDays.HasFlag(day) == true;
    }

    internal void SetWeeklyDay(ScheduleDays day, bool selected)
    {
        if (SelectedTask == null)
        {
            return;
        }

        var nextDays = selected
            ? SelectedTask.WeeklyDays | day
            : SelectedTask.WeeklyDays & ~day;

        if (SelectedTask.WeeklyDays == nextDays)
        {
            return;
        }

        _useCustomWeeklyDays = true;
        SelectedTask.WeeklyDays = nextDays;
        OnPropertyChanged(nameof(SelectedWeeklyPreset));
        OnWeeklyDaySelectionChanged();
    }

    private void OnWeeklyDaySelectionChanged()
    {
        OnPropertyChanged(nameof(IsWeeklyCustomSelected));
        OnPropertyChanged(nameof(WeeklyDayOptions));
        foreach (var option in WeeklyDayOptions)
        {
            option.RefreshSelection();
        }
    }
    
    [RelayCommand]
    private void AddTask()
    {
        var task = new ScheduledTask
        {
            Name = string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_DefaultTaskName"], Tasks.Count + 1),
            Type = ScheduleType.Interval,
            IntervalValue = 30,
            IntervalUnit = IntervalUnit.Seconds
        };
        _schedulerService.AddTask(task);
        SelectedTask = task;
        OnPropertyChanged(nameof(TaskCountText));
    }
    
    [RelayCommand]
    private async Task RemoveTaskAsync(ScheduledTask? task)
    {
        if (task == null) return;
        
        var confirmed = await _dialogService.ShowConfirmationAsync(
            _localizationService["Schedule_DeleteTitle"],
            string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_DeleteMessage"], task.Name));
            
        if (!confirmed) return;
        
        _schedulerService.RemoveTask(task.Id);
        if (SelectedTask?.Id == task.Id)
        {
            SelectedTask = Tasks.FirstOrDefault();
        }
        await SaveChangesAsync(showSuccessStatus: false);
        OnPropertyChanged(nameof(TaskCountText));
    }
    
    [RelayCommand]
    private void SelectTask(ScheduledTask? task)
    {
        if (task != null)
        {
            SelectedTask = task;
        }
    }
    
    [RelayCommand]
    private async Task BrowseMacroAsync()
    {
        if (SelectedTask == null) return;
        
        var filters = new FileDialogFilter[]
        {
            new FileDialogFilter { Name = _localizationService["Schedule_OpenMacroDialogFilter"], Extensions = new[] { "macro" } }
        };
        
        var filePath = await _dialogService.ShowOpenFileDialogAsync(
            _localizationService["Schedule_OpenMacroDialogTitle"],
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
                RaiseStatus(_localizationService["Schedule_StatusChangesSaved"]);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ScheduleViewModel] Failed to save scheduled tasks");
            var status = string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_StatusSaveFailed"], ex.Message);
            RaiseStatus(status);
            try
            {
                await _dialogService.ShowMessageAsync(_localizationService["Schedule_SaveFailedTitle"], status);
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
            RaiseStatus(_localizationService["Schedule_StatusExtensionWarning"]);
        }

        _schedulerService.SetTaskEnabled(task.Id, task.IsEnabled);
    }

    [RelayCommand]
    private async Task TaskEnabledChangedAsync(ScheduledTask task)
    {
        OnTaskEnabledChanged(task);
        await SaveChangesAsync(showSuccessStatus: false);
    }
    
    private void OnTaskStarting(object? sender, ScheduledTask task)
    {
        Dispatcher.UIThread.Post(() =>
        {
            RaiseStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_StatusRunning"], task.Name));
            
            // Refresh the selected task to update status display
            if (SelectedTask?.Id == task.Id)
            {
                OnPropertyChanged(nameof(SelectedTask));
                OnSelectedTaskStatusChanged();
            }
        });
    }
    
    private void OnTaskExecuted(object? sender, TaskExecutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Update global status
            var statusText = e.Success 
                ? string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_StatusCompleted"], e.Task.Name)
                : string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_StatusFailedExecution"], e.Task.Name, e.Message);
            RaiseStatus(statusText);
            
            // Refresh the selected task to update LastRunTime display
            if (SelectedTask?.Id == e.Task.Id)
            {
                OnPropertyChanged(nameof(SelectedTask));
                OnSelectedTaskStatusChanged();
            }
        });
    }

    private void OnSelectedTaskStatusChanged()
    {
        OnPropertyChanged(nameof(SelectedLastRunText));
        OnPropertyChanged(nameof(SelectedNextRunText));
        OnPropertyChanged(nameof(SelectedStatusText));
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
        OnPropertyChanged(nameof(Tasks));
        OnPropertyChanged(nameof(TaskCountText));
        OnPropertyChanged(nameof(SelectedMacroFileName));
        OnPropertyChanged(nameof(IntervalUnitOptions));
        OnPropertyChanged(nameof(SelectedIntervalUnit));
        OnPropertyChanged(nameof(WeeklyPresetOptions));
        OnPropertyChanged(nameof(WeeklyDayOptions));
        OnPropertyChanged(nameof(SelectedWeeklyPreset));
        OnPropertyChanged(nameof(SelectedTask));
        OnWeeklyDaySelectionChanged();
        OnSelectedTaskStatusChanged();
    }

    private void OnTasksCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Tasks));
        OnPropertyChanged(nameof(TaskCountText));
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        // Unsubscribe from events to prevent memory leaks
        _schedulerService.TaskStarting -= OnTaskStarting;
        _schedulerService.TaskExecuted -= OnTaskExecuted;
        if (_selectedTask != null)
        {
            _selectedTask.PropertyChanged -= OnSelectedTaskPropertyChanged;
        }
        _schedulerService.Tasks?.CollectionChanged -= OnTasksCollectionChanged;
        _localizationService.CultureChanged -= OnCultureChanged;
    }

}

public sealed record IntervalUnitOption(IntervalUnit Value, string DisplayName);

public sealed record WeeklyPresetOption(ScheduleDays? Value, string DisplayName);

public sealed class WeeklyDayOption : ViewModelBase
{
    private readonly ScheduleViewModel _owner;

    public WeeklyDayOption(ScheduleViewModel owner, ScheduleDays value, string displayName)
    {
        _owner = owner;
        Value = value;
        DisplayName = displayName;
    }

    public ScheduleDays Value { get; }

    public string DisplayName { get; }

    public bool IsSelected
    {
        get => _owner.HasWeeklyDay(Value);
        set => _owner.SetWeeklyDay(Value, value);
    }

    internal void RefreshSelection()
    {
        OnPropertyChanged(nameof(IsSelected));
    }
}
