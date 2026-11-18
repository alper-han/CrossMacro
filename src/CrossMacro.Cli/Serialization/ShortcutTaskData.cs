
namespace CrossMacro.Cli.Serialization;

public sealed record ShortcutTaskData(
    Guid Id,
    string Name,
    bool Enabled,
    IReadOnlyList<string> Hotkeys,
    string MacroFilePath,
    double PlaybackSpeed,
    bool LoopEnabled,
    bool RunWhileHeld,
    int RepeatCount,
    int RepeatDelayMs,
    bool RandomRepeatDelay,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RepeatDelayMinMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RepeatDelayMaxMs,
    IReadOnlyList<ShortcutWindowRule> WindowRules,
    DateTime? LastTriggeredTime,
    string? LastStatus,
    bool RunOnPressAndRelease = false,
    string ReleaseMacroFilePath = ""
);
