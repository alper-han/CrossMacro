namespace CrossMacro.UI.ViewModels.Automation;

/// <summary>UI editing buffer for a persisted Core trigger task.</summary>
public sealed partial class TriggerTaskEditor() : TaskEditorBase<TriggerTask>("New Trigger")
{
    [ObservableProperty] private TriggerField field = TriggerField.WindowClass;
    [ObservableProperty] private TriggerMatchMode matchMode = TriggerMatchMode.Contains;
    [ObservableProperty] private string value = string.Empty;
    [ObservableProperty] private TriggerOperation action = TriggerOperation.SwitchProfile;
    [ObservableProperty] private string targetProfileId = string.Empty;
    [ObservableProperty] private string macroFilePath = string.Empty;
    [ObservableProperty] private TriggerFireMode fireMode = TriggerFireMode.OnceOnChange;
    [ObservableProperty] private int? cooldownMs;
    [ObservableProperty] private int? debounceMs;
    [ObservableProperty] private bool isEnabled;
    [ObservableProperty] private DateTime? lastTriggeredTime;
    [ObservableProperty] private string? lastStatus;

    public bool CanBeEnabled =>
        (Field is TriggerField.None || !string.IsNullOrEmpty(Value))
        && (Action is not TriggerOperation.SwitchProfile || !string.IsNullOrEmpty(TargetProfileId))
        && (Action is not TriggerOperation.RunMacro || !string.IsNullOrEmpty(MacroFilePath));

    protected override void LoadCore(TriggerTask source)
    {
        Id = source.Id; Name = source.Name; Field = source.Field; MatchMode = source.MatchMode;
        Value = source.Value; Action = source.Action; TargetProfileId = source.TargetProfileId;
        MacroFilePath = source.MacroFilePath; FireMode = source.FireMode; CooldownMs = source.CooldownMs;
        DebounceMs = source.DebounceMs; IsEnabled = source.IsEnabled;
        LastTriggeredTime = source.LastTriggeredTime; LastStatus = source.LastStatus;
        NotifyCanBeEnabledChanged();
    }

    public override TriggerTask ToCore()
    {
        var task = new TriggerTask
        {
            Id = Id,
            Name = Name,
            Field = Field,
            MatchMode = MatchMode,
            Value = Value,
            Action = Action,
            TargetProfileId = TargetProfileId,
            MacroFilePath = MacroFilePath,
            FireMode = FireMode,
            CooldownMs = CooldownMs,
            DebounceMs = DebounceMs,
            IsEnabled = IsEnabled,
            LastTriggeredTime = LastTriggeredTime,
            LastStatus = LastStatus,
        };
        if (task.IsEnabled && !task.TrySetEnabled(enabled: true))
        {
            task.IsEnabled = false;
        }

        return task;
    }

    public void SyncRuntimeStatus(DateTime? timestamp, string? status)
    {
        ApplyPersistedState(() =>
        {
            LastTriggeredTime = timestamp;
            LastStatus = status;
        });
    }

    protected override bool IsConfigurationProperty(string? propertyName) =>
        propertyName is not (nameof(CanBeEnabled) or nameof(LastTriggeredTime) or nameof(LastStatus));

    partial void OnValueChanged(string value) => NotifyCanBeEnabledChanged();
    partial void OnFieldChanged(TriggerField value) => NotifyCanBeEnabledChanged();
    partial void OnActionChanged(TriggerOperation value) => NotifyCanBeEnabledChanged();
    partial void OnTargetProfileIdChanged(string value) => NotifyCanBeEnabledChanged();
    partial void OnMacroFilePathChanged(string value) => NotifyCanBeEnabledChanged();
    private void NotifyCanBeEnabledChanged() => OnPropertyChanged(nameof(CanBeEnabled));
}
