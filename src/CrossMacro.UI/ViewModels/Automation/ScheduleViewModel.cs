
namespace CrossMacro.UI.ViewModels.Automation;

/// <summary>
/// ViewModel for the Schedule tab - manages scheduled macro tasks
/// </summary>
public partial class ScheduleViewModel : ViewModelBase, IDisposable
{
    private readonly ISchedulerService _schedulerService;
    private readonly IDialogService _dialogService;
    private readonly TimeProvider _timeProvider;
    private readonly ILocalizationService _localizationService;
    private readonly IManageSchedule _manageSchedule;
    private readonly Lock _initializeLock = new();
    private Task? _initializeTask;
    private readonly ScopedTaskProjection<ScheduledTask, ScheduledTaskEditor> _projection;
    private bool _isIntervalSelected = true;
    private bool _isDateTimeSelected;
    private bool _isWeeklySelected;
    private bool _disposed;

    public ObservableCollection<ScheduledTaskEditor> Tasks => _projection.Items;

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
        IManageSchedule manageSchedule,
        ISchedulerService schedulerService,
        IDialogService dialogService,
        TimeProvider timeProvider,
        ILocalizationService localizationService,
        IProfileRuntimeState? profileRuntimeState = null,
        IUiDispatcher? uiDispatcher = null)
        : base(uiDispatcher)
    {
        _manageSchedule = manageSchedule ?? throw new ArgumentNullException(nameof(manageSchedule));
        _schedulerService = schedulerService ?? throw new ArgumentNullException(nameof(schedulerService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _projection = new ScopedTaskProjection<ScheduledTask, ScheduledTaskEditor>(
            _manageSchedule.ListAsync, UiDispatcher, static task => task.Id,
            static scope => new ScheduledTaskEditor { ScopeGeneration = scope },
            static (editor, task) => editor.Load(task),
            () => SelectedTask, selected => SelectedTask = selected,
            () => { OnPropertyChanged(nameof(Tasks)); OnPropertyChanged(nameof(TaskCountText)); });
        _ = profileRuntimeState; // Initialization now uses a scoped Application snapshot.
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

    public void RefreshProfileData()
    {
        if (_disposed) { return; }
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
            await RefreshEditorsAsync(propagateError: true).ConfigureAwait(false);
            await RunOnUiThreadAsync(() => { if (!_disposed) { SelectedTask = Tasks.FirstOrDefault(); } }).ConfigureAwait(false);
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
        var scope = _projection.ScopeGeneration;
        var task = new ScheduledTask
        {
            Name = string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_DefaultTaskName"], Tasks.Count + 1),
            Type = ScheduleType.Interval,
            IntervalValue = 30,
            IntervalUnit = IntervalUnit.Seconds,
        };
        await PersistMutationAsync(async () => { _ = await _manageSchedule.AddAsync(task, scope, CancellationToken.None).ConfigureAwait(false); }, selectTaskId: task.Id).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RemoveTaskAsync(ScheduledTaskEditor? task)
    {
        if (task is null)
        {
            return;
        }

        var scope = task.ScopeGeneration;
        var confirmed = await _dialogService.ShowConfirmationAsync(
            _localizationService["Schedule_DeleteTitle"],
            string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_DeleteMessage"], task.Name)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        await PersistMutationAsync(async () => { _ = await _manageSchedule.RemoveAsync(new TaskRequest(task.Id, ExpectedScopeGeneration: scope), CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);
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
        var selectedTask = SelectedTask;
        if (selectedTask is null)
        {
            return;
        }

        var scope = selectedTask.ScopeGeneration;
        var filters = new FileDialogFilter[]
        {
            new FileDialogFilter { Name = _localizationService["Schedule_OpenMacroDialogFilter"], Extensions = ["macro"] },
        };

        var filePath = await _dialogService.ShowOpenFileDialogAsync(
            _localizationService["Schedule_OpenMacroDialogTitle"],
            filters).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(filePath))
        {
            await RunOnUiThreadAsync(() =>
            {
                if (_disposed || scope != _projection.ScopeGeneration || !ReferenceEquals(SelectedTask, selectedTask))
                { RaiseStatus("The active profile or selected task changed. Reopen the file picker and try again."); return; }
                selectedTask.MacroFilePath = filePath;
                OnPropertyChanged(nameof(SelectedMacroFilePath));
                OnPropertyChanged(nameof(SelectedMacroFileName));
                OnPropertyChanged(nameof(SelectedTask));
            }).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await SaveChangesAsync(showSuccessStatus: true).ConfigureAwait(false);
    }

    private Task SaveChangesAsync(bool showSuccessStatus)
    {
        var selected = SelectedTask;
        var draft = selected?.ToCore();
        var scope = selected?.ScopeGeneration ?? _projection.ScopeGeneration;
        return draft is null ? Task.CompletedTask : PersistMutationAsync(
            async () => { _ = await _manageSchedule.UpdateAsync(draft, scope, CancellationToken.None).ConfigureAwait(false); }, showSuccessStatus);
    }

    private async Task PersistMutationAsync(Func<Task> mutation, bool showSuccessStatus = false, Guid? selectTaskId = null)
    {
        if (_disposed) { return; }
        var operationScope = _projection.ScopeGeneration;
        var selectedTaskId = selectTaskId ?? SelectedTask?.Id;
        try
        {
            await mutation().ConfigureAwait(false);
            if (_disposed) { return; }
            await RefreshEditorsAsync().ConfigureAwait(false);
            await RunOnUiThreadAsync(() =>
            {
                if (_disposed || operationScope != _projection.ScopeGeneration) { return; }
                SelectedTask = selectedTaskId is Guid id
                    ? Tasks.FirstOrDefault(candidate => candidate.Id == id) ?? Tasks.FirstOrDefault()
                    : Tasks.FirstOrDefault();
                OnPropertyChanged(nameof(TaskCountText));
                if (showSuccessStatus) { RaiseStatus(_localizationService["Schedule_StatusChangesSaved"]); }
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (_disposed) { return; }
            if (ex is TaskScopeConflictException) { await RefreshEditorsAsync().ConfigureAwait(false); }
            Log.LogError(ex, "[ScheduleViewModel] Failed to save scheduled tasks");
            var status = string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_StatusSaveFailed"], ex.Message);
            await RunOnUiThreadAsync(() =>
            {
                if (_disposed) { return; }
                RemapEditors();
                RaiseStatus(status);
            }).ConfigureAwait(false);
            try
            {
                if (_disposed) { return; }
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

    }

    [RelayCommand]
    private async Task TaskEnabledChangedAsync(ScheduledTaskEditor task)
    {
        OnTaskEnabledChanged(task);
        await PersistMutationAsync(async () => { _ = await _manageSchedule.SetEnabledAsync(new TaskRequest(task.Id, task.IsEnabled, task.ScopeGeneration), CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);
    }

    private void OnTaskStarting(object? sender, ScheduledTaskStartingEventArgs e)
    {
        var task = e.Task;
        UiDispatcher.Post(() =>
        {
            if (_disposed || !_schedulerService.IsCurrentTask(e.Task)) { return; }
            RaiseStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_StatusRunning"], task.Name));

            // Refresh the selected task to update status display
            if (SelectedTask?.Id == task.Id)
            {
                if (_projection.TryGetEditor(task.Id, out var editor))
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
        UiDispatcher.Post(() =>
        {
            if (_disposed || !_schedulerService.IsCurrentTask(e.Task)) { return; }
            // Update global status
            var statusText = e.Success
                ? string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_StatusCompleted"], e.Task.Name)
                : string.Format(_localizationService.CurrentCulture, _localizationService["Schedule_StatusFailedExecution"], e.Task.Name, e.Message);
            RaiseStatus(statusText);

            // Refresh the selected task to update LastRunTime display
            if (SelectedTask?.Id == e.Task.Id)
            {
                if (_projection.TryGetEditor(e.Task.Id, out var editor))
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
        if (UiDispatcher.CheckAccess())
        {
            StatusChanged?.Invoke(this, message);
            return;
        }

        UiDispatcher.Post(() => StatusChanged?.Invoke(this, message));
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

    private void RemapEditors() => _ = RefreshEditorsAsync();

    internal async Task RefreshEditorsAsync(bool propagateError = false)
    {
        try { await _projection.RefreshAsync().ConfigureAwait(false); }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (propagateError) { throw; }
            Log.LogError(error, "[ScheduleViewModel] Failed to refresh task projection");
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
        _projection.Dispose();

        // Unsubscribe from events to prevent memory leaks
        _schedulerService.TaskStarting -= OnTaskStarting;
        _schedulerService.TaskExecuted -= OnTaskExecuted;
        SelectedTask?.PropertyChanged -= OnSelectedTaskPropertyChanged;
        _schedulerService.Tasks?.CollectionChanged -= OnTasksCollectionChanged;
        _localizationService.CultureChanged -= OnCultureChanged;
    }

}
