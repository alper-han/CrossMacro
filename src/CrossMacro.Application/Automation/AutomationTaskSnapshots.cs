namespace CrossMacro.Application.Automation;

/// <summary>Detached task values used across the editing and persistence boundary.</summary>
public static class AutomationTaskSnapshots
{
    public static ScheduledTask Copy(ScheduledTask source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new ScheduledTask();
        CopyTo(source, result);
        return result;
    }

    public static ShortcutTask Copy(ShortcutTask source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new ShortcutTask();
        CopyTo(source, result);
        return result;
    }

    public static TriggerTask Copy(TriggerTask source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new TriggerTask();
        CopyTo(source, result);
        return result;
    }

    public static void CopyTo(ScheduledTask source, ScheduledTask destination)
    {
        ApplyConfiguration(source, destination);
        destination.LastRunTime = source.LastRunTime;
        destination.LastStatus = source.LastStatus;
        destination.NextRunTime = source.NextRunTime;
    }

    public static void CopyTo(ShortcutTask source, ShortcutTask destination)
    {
        ApplyConfiguration(source, destination);
        destination.LastStatus = source.LastStatus;
        destination.LastTriggeredTime = source.LastTriggeredTime;
    }

    public static void CopyTo(TriggerTask source, TriggerTask destination)
    {
        ApplyConfiguration(source, destination);
        destination.LastTriggeredTime = source.LastTriggeredTime;
        destination.LastStatus = source.LastStatus;
    }

    public static void ApplyConfiguration(ScheduledTask source, ScheduledTask destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (ReferenceEquals(source, destination))
        {
            return;
        }

        var nextRunTime = HasSameSchedule(source, destination) ? destination.NextRunTime : source.NextRunTime;
        destination.Id = source.Id;
        destination.Name = source.Name;
        destination.MacroFilePath = source.MacroFilePath;
        destination.Type = source.Type;
        destination.PlaybackSpeed = source.PlaybackSpeed;
        destination.IntervalValue = source.IntervalValue;
        destination.IntervalUnit = source.IntervalUnit;
        destination.UseRandomIntervalDelay = source.UseRandomIntervalDelay;
        destination.IntervalMaxValue = source.IntervalMaxValue;
        destination.IntervalMinValue = source.IntervalMinValue;
        destination.ScheduledDateTime = source.ScheduledDateTime;
        destination.WeeklyDays = source.WeeklyDays;
        destination.WeeklyTime = source.WeeklyTime;
        destination.IsEnabled = source.IsEnabled;
        destination.NextRunTime = nextRunTime;
    }

    public static void ApplyConfiguration(ShortcutTask source, ShortcutTask destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (ReferenceEquals(source, destination))
        {
            return;
        }

        destination.Id = source.Id;
        destination.Name = source.Name;
        destination.MacroFilePath = source.MacroFilePath;
        destination.HotkeyString = source.HotkeyString;
        destination.PlaybackSpeed = source.PlaybackSpeed;
        destination.LoopEnabled = source.LoopEnabled;
        destination.RepeatCount = source.RepeatCount;
        destination.RepeatDelayMs = source.RepeatDelayMs;
        destination.UseRandomRepeatDelay = source.UseRandomRepeatDelay;
        destination.RepeatDelayMinMs = source.RepeatDelayMinMs;
        destination.RepeatDelayMaxMs = source.RepeatDelayMaxMs;
        destination.RunWhileHeld = source.RunWhileHeld;
        destination.IsEnabled = source.IsEnabled;
        destination.WindowRules.Clear();
        foreach (var rule in source.WindowRules)
        {
            destination.WindowRules.Add(new ShortcutWindowRule { Field = rule.Field, MatchMode = rule.MatchMode, Value = rule.Value });
        }
    }

    public static void ApplyConfiguration(TriggerTask source, TriggerTask destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (ReferenceEquals(source, destination))
        {
            return;
        }

        destination.Id = source.Id;
        destination.Name = source.Name;
        destination.Field = source.Field;
        destination.MatchMode = source.MatchMode;
        destination.Value = source.Value;
        destination.Action = source.Action;
        destination.TargetProfileId = source.TargetProfileId;
        destination.MacroFilePath = source.MacroFilePath;
        destination.FireMode = source.FireMode;
        destination.CooldownMs = source.CooldownMs;
        destination.DebounceMs = source.DebounceMs;
        destination.IsEnabled = source.IsEnabled;
    }

    public static bool HasSameConfiguration(ScheduledTask left, ScheduledTask right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return HasSameSchedule(left, right) && string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            && left.PlaybackSpeed.CompareTo(right.PlaybackSpeed) is 0;
    }

    public static bool HasSameConfiguration(ShortcutTask left, ShortcutTask right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            && string.Equals(left.MacroFilePath, right.MacroFilePath, StringComparison.Ordinal)
            && string.Equals(left.HotkeyString, right.HotkeyString, StringComparison.Ordinal)
            && left.PlaybackSpeed.CompareTo(right.PlaybackSpeed) is 0 && left.LoopEnabled == right.LoopEnabled
            && left.RepeatCount == right.RepeatCount && left.RepeatDelayMs == right.RepeatDelayMs
            && left.UseRandomRepeatDelay == right.UseRandomRepeatDelay
            && left.RepeatDelayMinMs == right.RepeatDelayMinMs && left.RepeatDelayMaxMs == right.RepeatDelayMaxMs
            && left.RunWhileHeld == right.RunWhileHeld && left.IsEnabled == right.IsEnabled
            && left.WindowRules.Select(rule => (rule.Field, rule.MatchMode, rule.Value))
                .SequenceEqual(right.WindowRules.Select(rule => (rule.Field, rule.MatchMode, rule.Value)));
    }

    public static bool HasSameConfiguration(TriggerTask left, TriggerTask right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            && left.Field == right.Field && left.MatchMode == right.MatchMode && string.Equals(left.Value, right.Value, StringComparison.Ordinal)
            && left.Action == right.Action && string.Equals(left.TargetProfileId, right.TargetProfileId, StringComparison.Ordinal)
            && string.Equals(left.MacroFilePath, right.MacroFilePath, StringComparison.Ordinal)
            && left.FireMode == right.FireMode && left.CooldownMs == right.CooldownMs
            && left.DebounceMs == right.DebounceMs && left.IsEnabled == right.IsEnabled;
    }

    public static bool HasSameSchedule(ScheduledTask left, ScheduledTask right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.IsEnabled == right.IsEnabled && string.Equals(left.MacroFilePath, right.MacroFilePath, StringComparison.Ordinal)
            && left.Type == right.Type && left.IntervalValue == right.IntervalValue && left.IntervalUnit == right.IntervalUnit
            && left.UseRandomIntervalDelay == right.UseRandomIntervalDelay
            && left.IntervalMinValue == right.IntervalMinValue && left.IntervalMaxValue == right.IntervalMaxValue
            && left.ScheduledDateTime == right.ScheduledDateTime && left.ScheduledDateTime?.Kind == right.ScheduledDateTime?.Kind && left.WeeklyDays == right.WeeklyDays && left.WeeklyTime == right.WeeklyTime;
    }
}
