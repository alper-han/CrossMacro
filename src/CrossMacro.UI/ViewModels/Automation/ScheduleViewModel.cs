
namespace CrossMacro.UI.ViewModels.Automation;

/// <summary>
/// ViewModel for the Schedule tab - manages scheduled macro tasks
/// </summary>
public partial class ScheduleViewModel : AutomationTaskTabViewModel<ScheduledTask, ScheduledTaskEditor>
{
    private readonly ISchedulerService _schedulerService;
    private readonly TimeProvider _timeProvider;
    private bool _isIntervalSelected = true;
    private bool _isDateTimeSelected;
    private bool _isWeeklySelected;

    public IReadOnlyList<IntervalUnitOption> IntervalUnitOptions { get; private set; } = [];

    public IReadOnlyList<WeeklyPresetOption> WeeklyPresetOptions { get; private set; } = [];

    public IReadOnlyList<WeeklyDayOption> WeeklyDayOptions { get; private set; } = [];

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

    private protected override void OnSelectedTaskChanged()
    {
        OnPropertyChanged(nameof(SelectedMacroFilePath));
        OnPropertyChanged(nameof(SelectedMacroFileName));
        OnPropertyChanged(nameof(SelectedIntervalUnit));
        UpdateScheduleTypeSelection();
        OnSelectedTaskStatusChanged();
    }

    private protected override void OnSelectedTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
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
            ? Localize("NoFileSelected")
            : Path.GetFileName(SelectedTask.MacroFilePath);

    public string SelectedLastRunText => SelectedTask?.LastRunTime?.ToLocalTime().ToString("G", LocalizationService.CurrentCulture)
        ?? Localize("Never");

    public string SelectedNextRunText => SelectedTask?.NextRunTime?.ToLocalTime().ToString("G", LocalizationService.CurrentCulture)
        ?? Localize("NotScheduled");

    public string SelectedStatusText => string.IsNullOrWhiteSpace(SelectedTask?.LastStatus)
        ? Localize("StatusPlaceholder")
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

    public ScheduleViewModel(
        IManageAutomationTasks<ScheduledTask> manageSchedule,
        ISchedulerService schedulerService,
        IDialogService dialogService,
        TimeProvider timeProvider,
        ILocalizationService localizationService,
        IUiDispatcher? uiDispatcher = null)
        : base(manageSchedule, ScheduleTaskKind.Instance, localizationService, dialogService, "Schedule", uiDispatcher)
    {
        _schedulerService = schedulerService ?? throw new ArgumentNullException(nameof(schedulerService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        BuildLocalizedOptions();

        _schedulerService.TaskStarting += OnTaskStarting;
        _schedulerService.TaskExecuted += OnTaskExecuted;
        _schedulerService.Tasks?.CollectionChanged += OnServiceTasksChanged;
    }

    private protected override ScheduledTask CreateTask(string name) => new()
    {
        Name = name,
        Type = ScheduleType.Interval,
        IntervalValue = 30,
        IntervalUnit = IntervalUnit.Seconds,
    };

    private protected override void OnTaskEnabledChanged(ScheduledTaskEditor task)
    {
        if (task.IsEnabled
            && !string.IsNullOrWhiteSpace(task.MacroFilePath)
            && !MacroFileFormat.HasMacroExtension(task.MacroFilePath))
        {
            ReportStatus(Localize("StatusExtensionWarning"));
        }
    }

    private void BuildLocalizedOptions()
    {
        IntervalUnitOptions =
        [
            new IntervalUnitOption(IntervalUnit.Seconds, Localize("Seconds")),
            new IntervalUnitOption(IntervalUnit.Minutes, Localize("Minutes")),
            new IntervalUnitOption(IntervalUnit.Hours, Localize("Hours")),
        ];
        WeeklyPresetOptions =
        [
            new WeeklyPresetOption(ScheduleDays.EveryDay, Localize("WeeklyEveryDay")),
            new WeeklyPresetOption(ScheduleDays.Weekdays, Localize("WeeklyWeekdays")),
            new WeeklyPresetOption(ScheduleDays.Weekends, Localize("WeeklyWeekends")),
            new WeeklyPresetOption(Value: null, Localize("WeeklyCustom")),
        ];
        WeeklyDayOptions =
        [
            new(this, ScheduleDays.Monday, Localize("Monday")),
            new(this, ScheduleDays.Tuesday, Localize("Tuesday")),
            new(this, ScheduleDays.Wednesday, Localize("Wednesday")),
            new(this, ScheduleDays.Thursday, Localize("Thursday")),
            new(this, ScheduleDays.Friday, Localize("Friday")),
            new(this, ScheduleDays.Saturday, Localize("Saturday")),
            new(this, ScheduleDays.Sunday, Localize("Sunday")),
        ];
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
        foreach (var option in WeeklyDayOptions)
        {
            option.RefreshSelection();
        }
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
    private Task BrowseMacroAsync() =>
        PickMacroFileAsync("OpenMacroDialogTitle", static (task, path) => task.MacroFilePath = path);

    private void OnTaskStarting(object? sender, ScheduledTaskStartingEventArgs e)
    {
        var task = e.Task;
        UiDispatcher.Post(() =>
        {
            if (IsDisposed || !_schedulerService.IsCurrentTask(task)) { return; }
            ReportStatus(FormatLocalized("StatusRunning", task.Name));
            ApplyRuntimeStatus(task, "Running...");
        });
    }

    private void OnTaskExecuted(object? sender, TaskExecutedEventArgs e)
    {
        UiDispatcher.Post(() =>
        {
            if (IsDisposed || !_schedulerService.IsCurrentTask(e.Task)) { return; }
            ReportStatus(e.Success
                ? FormatLocalized("StatusCompleted", e.Task.Name)
                : FormatLocalized("StatusFailedExecution", e.Task.Name, e.Message));
            ApplyRuntimeStatus(e.Task, e.Task.LastStatus);
        });
    }

    private void ApplyRuntimeStatus(ScheduledTask task, string? status)
    {
        if (SelectedTask?.Id != task.Id)
        {
            return;
        }

        if (TryGetEditor(task.Id, out var editor))
        {
            editor.SyncRuntimeStatus(task.LastRunTime, task.NextRunTime, status, task.IsEnabled);
        }

        OnPropertyChanged(nameof(SelectedTask));
        OnSelectedTaskStatusChanged();
    }

    private void OnSelectedTaskStatusChanged()
    {
        OnPropertyChanged(nameof(SelectedLastRunText));
        OnPropertyChanged(nameof(SelectedNextRunText));
        OnPropertyChanged(nameof(SelectedStatusText));
    }

    private protected override void OnCultureChangedOnUiThread()
    {
        BuildLocalizedOptions();
        OnPropertyChanged(nameof(SelectedMacroFileName));
        OnPropertyChanged(nameof(IntervalUnitOptions));
        OnPropertyChanged(nameof(SelectedIntervalUnit));
        OnPropertyChanged(nameof(WeeklyPresetOptions));
        OnPropertyChanged(nameof(WeeklyDayOptions));
        OnPropertyChanged(nameof(SelectedWeeklyPreset));
        OnWeeklyDaySelectionChanged();
        OnSelectedTaskStatusChanged();
        base.OnCultureChangedOnUiThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            _schedulerService.TaskStarting -= OnTaskStarting;
            _schedulerService.TaskExecuted -= OnTaskExecuted;
            _schedulerService.Tasks?.CollectionChanged -= OnServiceTasksChanged;
        }

        base.Dispose(disposing);
    }
}
