namespace CrossMacro.UI.ViewModels;

/// <summary>UI editing buffer for a persisted Core scheduled task.</summary>
public sealed partial class ScheduledTaskEditor : ObservableObject
{
    private ScheduledTask? _source;
    private bool _isSyncingRuntimeStatus;

    [ObservableProperty] private Guid id;
    [ObservableProperty] private string name = "New Task";
    [ObservableProperty] private string macroFilePath = string.Empty;
    [ObservableProperty] private ScheduleType type = ScheduleType.Interval;
    [ObservableProperty] private double playbackSpeed = PlaybackOptions.DefaultSpeedMultiplier;
    [ObservableProperty] private bool isEnabled;
    [ObservableProperty] private int intervalValue = 30;
    [ObservableProperty] private IntervalUnit intervalUnit = IntervalUnit.Seconds;
    [ObservableProperty] private bool useRandomIntervalDelay;
    [ObservableProperty] private int intervalMinValue = 1;
    [ObservableProperty] private int intervalMaxValue = 30;
    [ObservableProperty] private DateTime? scheduledDateTime;
    [ObservableProperty] private ScheduleDays weeklyDays = ScheduleDays.Weekdays;
    [ObservableProperty] private TimeSpan weeklyTime = new(9, 0, 0);
    [ObservableProperty] private DateTime? lastRunTime;
    [ObservableProperty] private DateTime? nextRunTime;
    [ObservableProperty] private string? lastStatus;

    public bool CanBeEnabled => !string.IsNullOrEmpty(MacroFilePath)
        && (Type is not ScheduleType.Weekly || WeeklyDays is not ScheduleDays.None);

    public void Load(ScheduledTask source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        Id = source.Id; Name = source.Name; MacroFilePath = source.MacroFilePath; Type = source.Type;
        PlaybackSpeed = source.PlaybackSpeed; IsEnabled = source.IsEnabled; IntervalValue = source.IntervalValue;
        IntervalUnit = source.IntervalUnit; UseRandomIntervalDelay = source.UseRandomIntervalDelay;
        IntervalMinValue = source.IntervalMinValue; IntervalMaxValue = source.IntervalMaxValue;
        ScheduledDateTime = source.ScheduledDateTime; WeeklyDays = source.WeeklyDays; WeeklyTime = source.WeeklyTime;
        LastRunTime = source.LastRunTime; NextRunTime = source.NextRunTime; LastStatus = source.LastStatus;
        NotifyConfigurationChanged();
    }

    public ScheduledTask ToCore()
    {
        var task = new ScheduledTask();
        ApplyToCore(task);
        return task;
    }

    public void ApplyToCore(ScheduledTask target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Id = Id; target.Name = Name; target.MacroFilePath = MacroFilePath; target.Type = Type;
        target.PlaybackSpeed = PlaybackSpeed; target.IntervalValue = IntervalValue; target.IntervalUnit = IntervalUnit;
        target.UseRandomIntervalDelay = UseRandomIntervalDelay; target.IntervalMinValue = IntervalMinValue;
        target.IntervalMaxValue = IntervalMaxValue; target.ScheduledDateTime = ScheduledDateTime;
        target.WeeklyDays = WeeklyDays; target.WeeklyTime = WeeklyTime;
        target.IsEnabled = IsEnabled;
        target.Normalize();
        if (IsEnabled && !target.TrySetEnabled(enabled: true))
        {
            target.IsEnabled = false;
        }

        // Runtime fields are persisted state owned by the scheduler. Apply them after
        // the enablement invariant above, because enabling recalculates NextRunTime.
        target.LastRunTime = LastRunTime;
        target.NextRunTime = NextRunTime;
        target.LastStatus = LastStatus;
    }

    public void Rollback()
    {
        if (_source is not null)
        {
            Load(_source);
        }
    }

    public void SyncRuntimeStatus(DateTime? lastRunTime, DateTime? nextRunTime, string? status, bool isEnabled)
    {
        LastRunTime = lastRunTime;
        NextRunTime = nextRunTime;
        LastStatus = status;
        _isSyncingRuntimeStatus = true;
        try
        {
            IsEnabled = isEnabled;
        }
        finally
        {
            _isSyncingRuntimeStatus = false;
        }
    }

    public void CalculateNextRunTime(DateTime now)
    {
        var task = ToCore();
        task.CalculateNextRunTime(now);
        NextRunTime = task.NextRunTime;
    }

    partial void OnMacroFilePathChanged(string value) => NotifyCanBeEnabledChanged();
    partial void OnTypeChanged(ScheduleType value) => NotifyCanBeEnabledChanged();
    partial void OnWeeklyDaysChanged(ScheduleDays value) => NotifyCanBeEnabledChanged();
    partial void OnIsEnabledChanged(bool value)
    {
        // Runtime status sync mirrors the authoritative scheduler state and must not
        // be re-coerced by the local editability guard.
        if (value && !CanBeEnabled && !_isSyncingRuntimeStatus) IsEnabled = false;
    }
    partial void OnPlaybackSpeedChanged(double value)
    {
        var normalized = PlaybackOptions.NormalizeSpeedMultiplier(value);
        if (Math.Abs(value - normalized) > double.Epsilon) PlaybackSpeed = normalized;
    }
    partial void OnIntervalValueChanged(int value)
    {
        if (value < 1) IntervalValue = 1;
    }
    partial void OnIntervalMinValueChanged(int value) => NormalizeIntervalRange();
    partial void OnIntervalMaxValueChanged(int value) => NormalizeIntervalRange();
    partial void OnWeeklyTimeChanged(TimeSpan value)
    {
        var normalized = value;
        if (value < TimeSpan.Zero)
        {
            normalized = TimeSpan.Zero;
        }
        else if (value >= TimeSpan.FromDays(1))
        {
            normalized = TimeSpan.FromTicks(TimeSpan.TicksPerDay - 1);
        }
        if (value != normalized) WeeklyTime = normalized;
    }

    private void NormalizeIntervalRange()
    {
        var min = Math.Max(1, IntervalMinValue);
        var max = Math.Max(min, IntervalMaxValue);
        if (IntervalMinValue != min)
        {
            IntervalMinValue = min;
        }

        if (IntervalMaxValue != max)
        {
            IntervalMaxValue = max;
        }
    }

    private void NotifyCanBeEnabledChanged() => OnPropertyChanged(nameof(CanBeEnabled));

    private void NotifyConfigurationChanged()
    {
        NotifyCanBeEnabledChanged();
        OnPropertyChanged(nameof(NextRunTime));
    }
}
