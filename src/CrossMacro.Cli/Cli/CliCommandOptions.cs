using System.Collections.Generic;

namespace CrossMacro.Cli;

public abstract record CliCommandOptions(bool JsonOutput, string? LogLevel = null);

public enum RecordCoordinateMode
{
    Auto = 0,
    Absolute = 1,
    Relative = 2
}

public sealed record MacroValidateCliOptions(string MacroFilePath, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record MacroInfoCliOptions(string MacroFilePath, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record PlayCliOptions(
    string MacroFilePath,
    double SpeedMultiplier = 1.0,
    bool Loop = false,
    int RepeatCount = 1,
    int RepeatDelayMs = 0,
    int CountdownSeconds = 0,
    int TimeoutSeconds = 0,
    bool DryRun = false,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record DoctorCliOptions(bool Verbose = false, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record SettingsGetCliOptions(string? Key = null, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record SettingsSetCliOptions(string Key, string Value, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record ScheduleListCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record ScheduleRunCliOptions(string TaskId, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record ShortcutListCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record ShortcutRunCliOptions(string TaskId, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record RecordCliOptions(
    string OutputFilePath,
    bool RecordMouse = true,
    bool RecordKeyboard = true,
    RecordCoordinateMode CoordinateMode = RecordCoordinateMode.Auto,
    bool SkipInitialZero = false,
    int DurationSeconds = 0,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record HeadlessCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record RunCliOptions(
    IReadOnlyList<string> Steps,
    string? StepFilePath = null,
    double SpeedMultiplier = 1.0,
    int CountdownSeconds = 0,
    int TimeoutSeconds = 0,
    bool DryRun = false,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
