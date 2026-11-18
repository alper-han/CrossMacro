
namespace CrossMacro.Cli.Serialization;

public sealed record ShortcutTaskRunData(
    Guid Id,
    string Name,
    bool Enabled,
    IReadOnlyList<string> Hotkeys,
    string MacroFilePath,
    DateTime? LastTriggeredTime,
    string? LastStatus,
    bool RunOnPressAndRelease = false,
    string ReleaseMacroFilePath = ""
);
