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
    [ObservableProperty] private ObservableCollection<ShortcutWindowRuleEditor> windowRules = [];
    [ObservableProperty] private DateTime? lastTriggeredTime;
    [ObservableProperty] private string? lastStatus;

    public bool CanBeEnabled => !string.IsNullOrEmpty(MacroFilePath)
        && !string.IsNullOrEmpty(HotkeyString)
        && WindowRules.All(rule => rule.IsValid);
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
        ArgumentNullException.ThrowIfNull(source);
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
        ReplaceWindowRules(source.WindowRules);
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
        ArgumentNullException.ThrowIfNull(target);
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
        target.WindowRules.Clear();
        foreach (var rule in WindowRules)
        {
            target.WindowRules.Add(rule.ToCore());
        }
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

    public void AddWindowRule()
    {
        var rule = new ShortcutWindowRuleEditor();
        rule.PropertyChanged += OnWindowRulePropertyChanged;
        WindowRules.Add(rule);
        if (IsEnabled)
        {
            IsEnabled = false;
        }
        NotifyCanBeEnabledChanged();
    }

    public void RemoveWindowRule(ShortcutWindowRuleEditor rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        rule.PropertyChanged -= OnWindowRulePropertyChanged;
        _ = WindowRules.Remove(rule);
        NotifyCanBeEnabledChanged();
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

    private void ReplaceWindowRules(IEnumerable<ShortcutWindowRule> rules)
    {
        foreach (var existingRule in WindowRules)
        {
            existingRule.PropertyChanged -= OnWindowRulePropertyChanged;
        }

        WindowRules.Clear();
        foreach (var sourceRule in rules.Where(rule => rule is not null))
        {
            var rule = new ShortcutWindowRuleEditor();
            rule.Load(sourceRule);
            rule.PropertyChanged += OnWindowRulePropertyChanged;
            WindowRules.Add(rule);
        }
    }

    private void OnWindowRulePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShortcutWindowRuleEditor.Field)
            or nameof(ShortcutWindowRuleEditor.MatchMode)
            or nameof(ShortcutWindowRuleEditor.Value)
            or nameof(ShortcutWindowRuleEditor.IsValid))
        {
            if (IsEnabled && WindowRules.Any(rule => !rule.IsValid))
            {
                IsEnabled = false;
            }
            NotifyCanBeEnabledChanged();
        }
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
