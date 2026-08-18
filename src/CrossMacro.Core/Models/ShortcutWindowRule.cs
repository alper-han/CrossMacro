namespace CrossMacro.Core.Models;

/// <summary>Restricts a shortcut to focused windows whose property matches this rule.</summary>
public class ShortcutWindowRule
{
    [System.Text.Json.Serialization.JsonPropertyName("field")]
    public TriggerField Field { get; set; } = TriggerField.WindowClass;

    [System.Text.Json.Serialization.JsonPropertyName("matchMode")]
    public TriggerMatchMode MatchMode { get; set; } = TriggerMatchMode.Contains;

    [System.Text.Json.Serialization.JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    public bool IsValid() => WindowRuleMatcher.IsValid(Field, MatchMode, Value);
}
