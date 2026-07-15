namespace CrossMacro.UI.ViewModels;

/// <summary>UI editing buffer for a persisted Core shortcut task.</summary>
public sealed partial class ShortcutTaskEditor : ObservableObject
{
    private ShortcutTask? _source;

    [ObservableProperty] private Guid id;
    [ObservableProperty] private string name = "New Shortcut";
    [ObservableProperty] private string macroFilePath = string.Empty;
    [ObservableProperty] private string hotkeyString = string.Empty;
    [ObservableProperty] private double playbackSpeed = PlaybackOptions.DefaultSpeedMultiplier;
    [ObservableProperty] private bool isEnabled;
    [ObservableProperty] private bool loopEnabled;
    [ObservableProperty] private int repeatCount;
    [ObservableProperty] private int repeatDelayMs;
    [ObservableProperty] private bool useRandomRepeatDelay;
    [ObservableProperty] private int repeatDelayMinMs;
    [ObservableProperty] private int repeatDelayMaxMs;
    [ObservableProperty] private bool runWhileHeld;
    [ObservableProperty] private DateTime? lastTriggeredTime;
    [ObservableProperty] private string? lastStatus;

    public bool CanBeEnabled => !string.IsNullOrEmpty(MacroFilePath) && !string.IsNullOrEmpty(HotkeyString);
    public bool IsLoopEnabled
    {
        get => LoopEnabled || RunWhileHeld;
        set
        {
            if (value == IsLoopEnabled)
            {
                return;
            }

            LoopEnabled = value;
            if (!value)
            {
                RunWhileHeld = false;
            }

            OnPropertyChanged();
        }
    }

    public void Load(ShortcutTask source)
    {
        _source = source;
        Id = source.Id;
        Name = source.Name;
        MacroFilePath = source.MacroFilePath;
        HotkeyString = source.HotkeyString;
        PlaybackSpeed = source.PlaybackSpeed;
        IsEnabled = source.IsEnabled;
        LoopEnabled = source.LoopEnabled;
        RepeatCount = source.RepeatCount;
        RepeatDelayMs = source.RepeatDelayMs;
        UseRandomRepeatDelay = source.UseRandomRepeatDelay;
        RepeatDelayMinMs = source.RepeatDelayMinMs;
        RepeatDelayMaxMs = source.RepeatDelayMaxMs;
        RunWhileHeld = source.RunWhileHeld;
        LastTriggeredTime = source.LastTriggeredTime;
        LastStatus = source.LastStatus;
        NotifyConfigurationChanged();
    }

    public ShortcutTask ToCore()
    {
        var task = new ShortcutTask();
        ApplyToCore(task);
        return task;
    }

    public void ApplyToCore(ShortcutTask target)
    {
        target.Id = Id;
        target.Name = Name;
        target.MacroFilePath = MacroFilePath;
        target.HotkeyString = HotkeyString;
        target.PlaybackSpeed = PlaybackSpeed;
        target.IsEnabled = IsEnabled;
        target.LoopEnabled = LoopEnabled;
        target.RepeatCount = RepeatCount;
        target.RepeatDelayMs = RepeatDelayMs;
        target.UseRandomRepeatDelay = UseRandomRepeatDelay;
        target.RepeatDelayMinMs = RepeatDelayMinMs;
        target.RepeatDelayMaxMs = RepeatDelayMaxMs;
        target.RunWhileHeld = RunWhileHeld;
        target.LastTriggeredTime = LastTriggeredTime;
        target.LastStatus = LastStatus;
        target.Normalize();
    }

    public void Rollback()
    {
        if (_source is not null)
        {
            Load(_source);
        }
    }

    public void SyncRuntimeStatus(DateTime? timestamp, string? status)
    {
        LastTriggeredTime = timestamp;
        LastStatus = status;
    }

    partial void OnMacroFilePathChanged(string value) => NotifyCanBeEnabledChanged();
    partial void OnHotkeyStringChanged(string value) => NotifyCanBeEnabledChanged();
    partial void OnIsEnabledChanged(bool value)
    {
        if (value && !CanBeEnabled) IsEnabled = false;
    }
    partial void OnPlaybackSpeedChanged(double value)
    {
        var normalized = PlaybackOptions.NormalizeSpeedMultiplier(value);
        if (Math.Abs(value - normalized) > double.Epsilon) PlaybackSpeed = normalized;
    }
    partial void OnRepeatDelayMsChanged(int value)
    {
        var normalized = PlaybackOptions.NormalizeDelayMs(value);
        if (value != normalized) RepeatDelayMs = normalized;
    }
    partial void OnLoopEnabledChanged(bool value) => NotifyLoopStateChanged();
    partial void OnRunWhileHeldChanged(bool value) => NotifyLoopStateChanged();
    partial void OnRepeatDelayMinMsChanged(int value) => NormalizeDelayRange();
    partial void OnRepeatDelayMaxMsChanged(int value) => NormalizeDelayRange();

    private void NotifyCanBeEnabledChanged()
    {
        OnPropertyChanged(nameof(CanBeEnabled));
    }

    private void NotifyLoopStateChanged()
    {
        if (LoopEnabled && RunWhileHeld)
        {
            if (RunWhileHeld)
            {
                LoopEnabled = false;
            }
            else
            {
                RunWhileHeld = false;
            }
        }
        OnPropertyChanged(nameof(IsLoopEnabled));
    }

    private void NormalizeDelayRange()
    {
        var (min, max) = PlaybackOptions.NormalizeDelayRange(RepeatDelayMinMs, RepeatDelayMaxMs);
        if (RepeatDelayMinMs != min)
        {
            RepeatDelayMinMs = min;
        }

        if (RepeatDelayMaxMs != max)
        {
            RepeatDelayMaxMs = max;
        }
    }

    private void NotifyConfigurationChanged()
    {
        NotifyCanBeEnabledChanged();
        OnPropertyChanged(nameof(IsLoopEnabled));
    }
}
