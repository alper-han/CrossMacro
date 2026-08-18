namespace CrossMacro.Cli.Options;

public sealed record ShortcutCliOptions(
    ShortcutCliAction Action,
    string? TaskId = null,
    string? Name = null,
    string? MacroFilePath = null,
    string? Hotkey = null,
    double? Speed = null,
    bool? Loop = null,
    int? RepeatCount = null,
    int? RepeatDelayMs = null,
    int? RepeatDelayMinMs = null,
    int? RepeatDelayMaxMs = null,
    bool RunWhileHeld = false,
    bool? Enabled = null,
    bool JsonOutput = false,
    string? LogLevel = null,
    IReadOnlyList<ShortcutWindowRule>? WindowRules = null,
    bool ClearWindowRules = false)
    : CliCommandOptions(JsonOutput, LogLevel);
