namespace CrossMacro.Core.Models.Automation.Shortcuts;

/// <summary>
/// Represents a shortcut-triggered macro task.
/// </summary>
public class ShortcutTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Shortcut";
    public string MacroFilePath { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonObjectCreationHandling(
        System.Text.Json.Serialization.JsonObjectCreationHandling.Populate)]
    public ICollection<string> Hotkeys { get; } = [];

    public double PlaybackSpeed { get; set; } = PlaybackOptions.DefaultSpeedMultiplier;
    public bool IsEnabled { get; set; }
    public bool CanBeEnabled => !string.IsNullOrEmpty(MacroFilePath)
        && Hotkeys.Any(static hotkey => !string.IsNullOrWhiteSpace(hotkey))
        && WindowRules.All(rule => rule is not null && rule.IsValid());
    public bool LoopEnabled { get; set; }
    public int RepeatCount { get; set; }
    public int RepeatDelayMs { get; set; }
    public bool UseRandomRepeatDelay { get; set; }
    public int RepeatDelayMinMs { get; set; }
    public int RepeatDelayMaxMs { get; set; }
    public bool RunWhileHeld { get; set; }

    [System.Text.Json.Serialization.JsonObjectCreationHandling(
        System.Text.Json.Serialization.JsonObjectCreationHandling.Populate)]
    public ICollection<ShortcutWindowRule> WindowRules { get; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsLoopEnabled => LoopEnabled || RunWhileHeld;

    public string? LastStatus { get; set; }
    public DateTime? LastTriggeredTime { get; set; }

    public bool TrySetEnabled(bool enabled)
    {
        if (enabled && !CanBeEnabled)
        {
            return false;
        }

        IsEnabled = enabled;
        return true;
    }

    public void Normalize()
    {
        PlaybackSpeed = PlaybackOptions.NormalizeSpeedMultiplier(PlaybackSpeed);
        RepeatDelayMs = PlaybackOptions.NormalizeDelayMs(RepeatDelayMs);
        (RepeatDelayMinMs, RepeatDelayMaxMs) = PlaybackOptions.NormalizeDelayRange(RepeatDelayMinMs, RepeatDelayMaxMs);
        NormalizeHotkeys();

        if (LoopEnabled)
        {
            RunWhileHeld = false;
        }
        else if (RunWhileHeld)
        {
            LoopEnabled = false;
        }

        if (IsEnabled && !CanBeEnabled)
        {
            IsEnabled = false;
        }
    }

    private void NormalizeHotkeys()
    {
        var normalized = Hotkeys
            .Where(static hotkey => !string.IsNullOrWhiteSpace(hotkey))
            .Select(static hotkey => hotkey.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Hotkeys.Clear();
        foreach (var hotkey in normalized)
        {
            Hotkeys.Add(hotkey);
        }
    }
}
