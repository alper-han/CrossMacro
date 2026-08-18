namespace CrossMacro.Core.Models;

/// <summary>A persisted window-state trigger and its runtime status.</summary>
public class TriggerTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Trigger";
    public TriggerField Field { get; set; } = TriggerField.WindowClass;
    public TriggerMatchMode MatchMode { get; set; } = TriggerMatchMode.Contains;
    public string Value { get; set; } = string.Empty;
    public TriggerOperation Action { get; set; } = TriggerOperation.SwitchProfile;
    public string TargetProfileId { get; set; } = string.Empty;
    public string MacroFilePath { get; set; } = string.Empty;
    public TriggerFireMode FireMode { get; set; } = TriggerFireMode.OnceOnChange;
    public int? CooldownMs { get; set; }
    public int? DebounceMs { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? LastTriggeredTime { get; set; }
    public string? LastStatus { get; set; }

    public bool CanBeEnabled => IsValidConfiguration();

    public bool IsValidConfiguration() =>
        (Field is TriggerField.None || !string.IsNullOrEmpty(Value))
        && (Action is not TriggerOperation.SwitchProfile || !string.IsNullOrEmpty(TargetProfileId))
        && (Action is not TriggerOperation.RunMacro || !string.IsNullOrEmpty(MacroFilePath));

    public bool TrySetEnabled(bool enabled)
    {
        if (enabled && !IsValidConfiguration())
        {
            return false;
        }

        IsEnabled = enabled;
        return true;
    }
}
