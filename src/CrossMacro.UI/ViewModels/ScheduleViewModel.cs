
namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Schedule tab - manages scheduled macro tasks
/// </summary>
public partial class ScheduleViewModel : ViewModelBase, IDisposable
{
    private readonly ISchedulerService _schedulerService;
    private readonly IDialogService _dialogService;
    private readonly TimeProvider _timeProvider;
    private readonly ILocalizationService _localizationService;
    private readonly IManageSchedule? _manageSchedule;
    private readonly IProfileRuntimeState? _profileRuntimeState;
    private readonly Lock _initializeLock = new();
    private Task? _initializeTask;
    private readonly Dictionary<Guid, ScheduledTaskEditor> _editors = [];
    private bool _isIntervalSelected = true;
    private bool _isDateTimeSelected;
    private bool _isWeeklySelected;
    private bool _disposed;

    public ObservableCollection<ScheduledTaskEditor> Tasks { get; } = [];

    public IReadOnlyList<IntervalUnitOption> IntervalUnitOptions =>
    [
        new IntervalUnitOption(IntervalUnit.Seconds, _localizationService["Schedule_Seconds"]),
        new IntervalUnitOption(IntervalUnit.Minutes, _localizationService["Schedule_Minutes"]),
        new IntervalUnitOption(IntervalUnit.Hours, _localizationService["Schedule_Hours"]),
    ];

    public IReadOnlyList<WeeklyPresetOption> WeeklyPresetOptions =>
    [
        new WeeklyPresetOption(ScheduleDays.EveryDay, _localizationService["Schedule_WeeklyEveryDay"]),
        new WeeklyPresetOption(ScheduleDays.Weekdays, _localizationService["Schedule_WeeklyWeekdays"]),
        new WeeklyPresetOption(ScheduleDays.Weekends, _localizationService["Schedule_WeeklyWeekends"]),
        new WeeklyPresetOption(Value: null, _localizationService["Schedule_WeeklyCustom"]),
    ];

    public IReadOnlyList<WeeklyDayOption> WeeklyDayOptions =>
    [
        new(this, ScheduleDays.Monday, _localizationService["Schedule_Monday"]),
        new(this, ScheduleDays.Tuesday, _localizationService["Schedule_Tuesday"]),
        new(this, ScheduleDays.Wednesday, _localizationService["Schedule_Wednesday"]),
        new(this, ScheduleDays.Thursday, _localizationService["Schedule_Thursday"]),
        new(this, ScheduleDays.Friday, _localizationService["Schedule_Friday"]),
        new(this, ScheduleDays.Saturday, _localizationService["Schedule_Saturday"]),
        new(this, ScheduleDays.Sunday, _localizationService["Schedule_Sunday"]),
    ];

    public string TaskCountText => string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_ItemsText"], Tasks.Count);

    public IntervalUnitOption? SelectedIntervalUnit
    {
        get => IntervalUnitOptions.FirstOrDefault(option => option.Value == SelectedTask?.IntervalUnit);
        set
        {
            if (SelectedTask is not null && value != null && SelectedTask.IntervalUnit != value.Value)
            {
                SelectedTask.IntervalUnit = value.Value;
                OnPropertyChanged();
            }
        }
    }

    public ScheduledTaskEditor? SelectedTask
    {
        get; set
        {
            if (field != value)
            {
                field?.PropertyChanged -= OnSelectedTaskPropertyChanged;

                field = value;
                field?.PropertyChanged += OnSelectedTaskPropertyChanged;
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
        if (e.PropertyName is nameof(ScheduledTaskEditor.LastRunTime) or nameof(ScheduledTaskEditor.NextRunTime) or nameof(ScheduledTaskEditor.LastStatus))
        {
            PostToUiThread(OnSelectedTaskStatusChanged);
            return;
        }

        if (sender is ScheduledTaskEditor task
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
            PostToUiThread(() =>
            {
                if (string.Equals(e.PropertyName, nameof(ScheduledTask.IntervalUnit), StringComparison.Ordinal))
                {
                    OnPropertyChanged(nameof(SelectedIntervalUnit));
                }

                if (string.Equals(e.PropertyName, nameof(ScheduledTask.WeeklyDays), StringComparison.Ordinal))
                {
                    SyncWeeklyPresetFromSelectedTask();
                    OnWeeklyDaySelectionChanged();
                    OnPropertyChanged(nameof(SelectedTask));
                }

                task.CalculateNextRunTime(_timeProvider.GetUtcNow().UtcDateTime);
                OnSelectedTaskStatusChanged();
            });
        }
    }

    public bool HasSelectedTask => SelectedTask is not null;

    public string? SelectedMacroFilePath
    {
        get => string.IsNullOrEmpty(SelectedTask?.MacroFilePath) ? null : SelectedTask.MacroFilePath;
        set
        {
            if (SelectedTask is not null && !string.Equals(SelectedTask.MacroFilePath, value ?? "", StringComparison.Ordinal))
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
        : SelectedTask.LastStatus;

    public bool IsIntervalSelected
    {
        get => _isIntervalSelected;
        set
        {
            if (_isIntervalSelected != value)
            {
                _isIntervalSelected = value;
                OnPropertyChanged();
                if (value && SelectedTask is not null)
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
                if (value && SelectedTask is not null)
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
                if (value && SelectedTask is not null)
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
        TimeProvider timeProvider,
        ILocalizationService localizationService,
        IProfileRuntimeState? profileRuntimeState = null)
    {
        _schedulerService = schedulerService ?? throw new ArgumentNullException(nameof(schedulerService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _localizationService = localizationService;
        _profileRuntimeState = profileRuntimeState;
        _localizationService.CultureChanged += OnCultureChanged;

        // Subscribe to task execution events
        _schedulerService.TaskStarting += OnTaskStarting;
        _schedulerService.TaskExecuted += OnTaskExecuted;
        _schedulerService.Tasks?.CollectionChanged += OnTasksCollectionChanged;
        RemapEditors();

    }

    public ScheduleViewModel(
        IManageSchedule manageSchedule,
        ISchedulerService schedulerService,
        IDialogService dialogService,
        TimeProvider timeProvider,
        ILocalizationService localizationService,
        IProfileRuntimeState? profileRuntimeState = null)
        : this(schedulerService, dialogService, timeProvider, localizationService, profileRuntimeState)
    {
        _manageSchedule = manageSchedule;
    }

    public Task InitializeAsync()
    {
        lock (_initializeLock)
        {
            _initializeTask ??= InitializeCoreAsync();
            return _initializeTask;
        }
    }

    public void RefreshProfileData()
    {
        RemapEditors();
        SelectedTask = Tasks.FirstOrDefault();
        OnPropertyChanged(nameof(Tasks));
        OnPropertyChanged(nameof(TaskCountText));
        OnPropertyChanged(nameof(SelectedTask));
        OnPropertyChanged(nameof(SelectedMacroFileName));
        OnPropertyChanged(nameof(SelectedIntervalUnit));
        OnPropertyChanged(nameof(SelectedWeeklyPreset));
        OnWeeklyDaySelectionChanged();
        OnSelectedTaskStatusChanged();
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            // ProfileRuntimeCoordinator owns the initial profile load before the shell is composed.
            if (_profileRuntimeState?.IsInitialized is not true)
            {
                await _schedulerService.LoadAsync().ConfigureAwait(false);
            }

            _schedulerService.Start();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var status = string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_StatusInitFailed"], ex.Message);
            await RunOnUiThreadAsync(() => RaiseStatus(status)).ConfigureAwait(false);
        }
    }

    public DateTimeOffset? ScheduledDate
    {
        get => SelectedTask?.ScheduledDateTime == null ? null : new DateTimeOffset(SelectedTask.ScheduledDateTime.Value);
        set
        {
            if (SelectedTask is not null && value is not null)
            {
                var current = SelectedTask.ScheduledDateTime ?? _timeProvider.GetUtcNow().LocalDateTime;
                // Preserve time, change date
                var newDateTime = value.Value.Date + current.TimeOfDay;

                if (SelectedTask.ScheduledDateTime != newDateTime)
                {
                    SelectedTask.ScheduledDateTime = newDateTime;
                    if (SelectedTask.IsEnabled)
                    {
                        SelectedTask.CalculateNextRunTime(_timeProvider.GetUtcNow().UtcDateTime);
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
            if (SelectedTask is not null && value is not null)
            {
                var current = SelectedTask.ScheduledDateTime ?? _timeProvider.GetUtcNow().LocalDateTime;
                // Preserve date, change time (including seconds)
                var newDateTime = current.Date + value.Value;

                if (SelectedTask.ScheduledDateTime != newDateTime)
                {
                    SelectedTask.ScheduledDateTime = newDateTime;
                    if (SelectedTask.IsEnabled)
                    {
                        SelectedTask.CalculateNextRunTime(_timeProvider.GetUtcNow().UtcDateTime);
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
            if (SelectedTask is not null && value is not null && SelectedTask.WeeklyTime != value.Value)
            {
                SelectedTask.WeeklyTime = value.Value;
                if (SelectedTask.IsEnabled)
                {
                    SelectedTask.CalculateNextRunTime(_timeProvider.GetUtcNow().UtcDateTime);
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
            if (SelectedTask is null)
            {
                return null;
            }

            if (IsWeeklyCustomSelected)
            {
                return WeeklyPresetOptions.FirstOrDefault(option => option.Value is null);
            }

            return WeeklyPresetOptions.FirstOrDefault(option => option.Value == SelectedTask.WeeklyDays)
                ?? WeeklyPresetOptions.FirstOrDefault(option => option.Value is null);
        }
        set
        {
            if (SelectedTask is null || value == null)
            {
                return;
            }

            if (value.Value is null)
            {
                IsWeeklyCustomSelected = true;
                OnPropertyChanged();
                OnWeeklyDaySelectionChanged();
                return;
            }

            IsWeeklyCustomSelected = false;
            if (SelectedTask.WeeklyDays != value.Value.Value)
            {
                SelectedTask.WeeklyDays = value.Value.Value;
            }

            OnPropertyChanged();
            OnWeeklyDaySelectionChanged();
        }
    }

    public bool IsWeeklyCustomSelected { get; private set; }

    private void UpdateScheduleTypeSelection()
    {
        if (SelectedTask is not null)
        {
            _isIntervalSelected = SelectedTask.Type is ScheduleType.Interval;
            _isDateTimeSelected = SelectedTask.Type is ScheduleType.SpecificTime;
            _isWeeklySelected = SelectedTask.Type is ScheduleType.Weekly;
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
        IsWeeklyCustomSelected = SelectedTask?.WeeklyDays is not (ScheduleDays.EveryDay or ScheduleDays.Weekdays or ScheduleDays.Weekends);
    }

    internal bool HasWeeklyDay(ScheduleDays day)
    {
        return (SelectedTask?.WeeklyDays.HasFlag(day)) is true;
    }

    internal void SetWeeklyDay(ScheduleDays day, bool selected)
    {
        if (SelectedTask is null)
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

        IsWeeklyCustomSelected = true;
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
    private async Task AddTaskAsync()
    {
        var task = new ScheduledTask
        {
            Name = string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_DefaultTaskName"], Tasks.Count + 1),
            Type = ScheduleType.Interval,
            IntervalValue = 30,
            IntervalUnit = IntervalUnit.Seconds,
        };
        if (_manageSchedule is not null)
        {
            _ = await _manageSchedule.AddAsync(task, default).ConfigureAwait(false);
        }
        else
        {
            _schedulerService.AddTask(task);
        }
        await RunOnUiThreadAsync(() =>
        {
            RemapEditors();
            SelectedTask = _editors[task.Id];
            OnPropertyChanged(nameof(TaskCountText));
        }).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RemoveTaskAsync(ScheduledTaskEditor? task)
    {
        if (task is null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmationAsync(
            _localizationService["Schedule_DeleteTitle"],
            string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_DeleteMessage"], task.Name)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        if (_manageSchedule is not null)
        {
            var selectedTaskId = SelectedTask?.Id;
            _ = await _manageSchedule.RemoveAsync(new TaskRequest(task.Id), default).ConfigureAwait(false);
            await RunOnUiThreadAsync(() =>
            {
                RemapEditors();
                SelectedTask = selectedTaskId is Guid id
                    ? Tasks.FirstOrDefault(candidate => candidate.Id == id) ?? Tasks.FirstOrDefault()
                    : Tasks.FirstOrDefault();
                OnPropertyChanged(nameof(TaskCountText));
            }).ConfigureAwait(false);
            return;
        }

        var coreTask = _schedulerService.Tasks.First(candidate => candidate.Id == task.Id);
        var wasSelected = SelectedTask?.Id == task.Id;
        await RunOnUiThreadAsync(() =>
        {
            _schedulerService.RemoveTask(task.Id);
            if (wasSelected)
            {
                SelectedTask = Tasks.FirstOrDefault();
            }
        }).ConfigureAwait(false);
        await SaveChangesAsync(showSuccessStatus: false, rollback: () =>
        {
            _schedulerService.AddTask(coreTask);
            if (wasSelected)
            {
                SelectedTask = task;
            }
        }).ConfigureAwait(false);
        await RunOnUiThreadAsync(() => OnPropertyChanged(nameof(TaskCountText))).ConfigureAwait(false);
    }

    [RelayCommand]
    private void SelectTask(ScheduledTaskEditor? task)
    {
        if (task is not null)
        {
            SelectedTask = task;
        }
    }

    [RelayCommand]
    private async Task BrowseMacroAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        var filters = new FileDialogFilter[]
        {
            new FileDialogFilter { Name = _localizationService["Schedule_OpenMacroDialogFilter"], Extensions = ["macro"] },
        };

        var filePath = await _dialogService.ShowOpenFileDialogAsync(
            _localizationService["Schedule_OpenMacroDialogTitle"],
            filters).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(filePath))
        {
            await RunOnUiThreadAsync(() => SelectedMacroFilePath = filePath).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await SaveChangesAsync(showSuccessStatus: true).ConfigureAwait(false);
    }

    private async Task SaveChangesAsync(bool showSuccessStatus, Action? rollback = null)
    {
        try
        {
            if (_manageSchedule is not null && SelectedTask is not null)
            {
                _ = await _manageSchedule.UpdateAsync(SelectedTask.ToCore(), default).ConfigureAwait(false);
            }
            else
            {
                await RunOnUiThreadAsync(() =>
                {
                    foreach (var editor in Tasks)
                    {
                        editor.ApplyToCore(_schedulerService.Tasks.First(task => task.Id == editor.Id));
                    }
                }).ConfigureAwait(false);
                await _schedulerService.SaveAsync().ConfigureAwait(false);
            }
            if (showSuccessStatus)
            {
                await RunOnUiThreadAsync(() => RaiseStatus(_localizationService["Schedule_StatusChangesSaved"])).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[ScheduleViewModel] Failed to save scheduled tasks");
            var status = string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_StatusSaveFailed"], ex.Message);
            await RunOnUiThreadAsync(() =>
            {
                SelectedTask?.Rollback();
                rollback?.Invoke();
                RaiseStatus(status);
            }).ConfigureAwait(false);
            try
            {
                await _dialogService.ShowMessageAsync(_localizationService["Schedule_SaveFailedTitle"], status).ConfigureAwait(false);
            }
            catch (Exception dialogEx) when (dialogEx is not OutOfMemoryException)
            {
                Log.Warning(dialogEx, "[ScheduleViewModel] Failed to show save error dialog");
            }
        }
    }

    public void OnTaskEnabledChanged(ScheduledTaskEditor task)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.IsEnabled &&
            !string.IsNullOrWhiteSpace(task.MacroFilePath) &&
            !task.MacroFilePath.EndsWith(".macro", StringComparison.OrdinalIgnoreCase))
        {
            RaiseStatus(_localizationService["Schedule_StatusExtensionWarning"]);
        }

        if (_manageSchedule is null)
        {
            _schedulerService.SetTaskEnabled(task.Id, task.IsEnabled);
        }
    }

    [RelayCommand]
    private async Task TaskEnabledChangedAsync(ScheduledTaskEditor task)
    {
        var previousEnabled = !task.IsEnabled;
        OnTaskEnabledChanged(task);
        if (_manageSchedule is not null)
        {
            var selectedTaskId = SelectedTask?.Id;
            try
            {
                _ = await _manageSchedule.SetEnabledAsync(new TaskRequest(task.Id, task.IsEnabled), default).ConfigureAwait(false);
            }
            finally
            {
                await RunOnUiThreadAsync(() =>
                {
                    RemapEditors();
                    SelectedTask = selectedTaskId is Guid id
                        ? Tasks.FirstOrDefault(candidate => candidate.Id == id)
                        : null;
                }).ConfigureAwait(false);
            }
            return;
        }

        await SaveChangesAsync(showSuccessStatus: false, rollback: () => _schedulerService.SetTaskEnabled(task.Id, previousEnabled)).ConfigureAwait(false);
    }

    private void OnTaskStarting(object? sender, ScheduledTaskStartingEventArgs e)
    {
        var task = e.Task;
        Dispatcher.UIThread.Post(() =>
        {
            RaiseStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_StatusRunning"], task.Name));

            // Refresh the selected task to update status display
            if (SelectedTask?.Id == task.Id)
            {
                if (_editors.TryGetValue(task.Id, out var editor))
                {
                    editor.SyncRuntimeStatus(task.LastRunTime, task.NextRunTime, "Running...", task.IsEnabled);
                }
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
                if (_editors.TryGetValue(e.Task.Id, out var editor))
                {
                    editor.SyncRuntimeStatus(e.Task.LastRunTime, e.Task.NextRunTime, e.Task.LastStatus, e.Task.IsEnabled);
                }
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

    private void RaiseStatus(string message)
    {
        if (Avalonia.Application.Current is null || Dispatcher.UIThread.CheckAccess())
        {
            StatusChanged?.Invoke(this, message);
            return;
        }

        Dispatcher.UIThread.Post(() => StatusChanged?.Invoke(this, message));
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        PostToUiThread(() =>
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
        });
    }

    private void OnTasksCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        PostToUiThread(() =>
        {
            RemapEditors();
            OnPropertyChanged(nameof(Tasks));
            OnPropertyChanged(nameof(TaskCountText));
        });
    }

    private void RemapEditors()
    {
        var tasks = _schedulerService.Tasks ?? [];
        var current = tasks.ToDictionary(task => task.Id);
        foreach (var task in tasks)
        {
            if (!_editors.TryGetValue(task.Id, out var editor))
            {
                editor = new ScheduledTaskEditor();
                _editors[task.Id] = editor;
            }
            editor.Load(task);
            if (!Tasks.Contains(editor))
            {
                Tasks.Add(editor);
            }
        }

        foreach (var editor in Tasks.Where(editor => !current.ContainsKey(editor.Id)).ToArray())
        {
            _ = Tasks.Remove(editor);
            _ = _editors.Remove(editor.Id);
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Unsubscribe from events to prevent memory leaks
        _schedulerService.TaskStarting -= OnTaskStarting;
        _schedulerService.TaskExecuted -= OnTaskExecuted;
        SelectedTask?.PropertyChanged -= OnSelectedTaskPropertyChanged;
        _schedulerService.Tasks?.CollectionChanged -= OnTasksCollectionChanged;
        _localizationService.CultureChanged -= OnCultureChanged;
    }

}
