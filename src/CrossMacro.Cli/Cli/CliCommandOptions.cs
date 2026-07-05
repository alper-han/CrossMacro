using System.Collections.Generic;
using CrossMacro.Platform.Abstractions;

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

public enum ClipboardCliAction
{
    Get,
    Set,
    Clear
}

public sealed record ClipboardCliOptions(
    ClipboardCliAction Action,
    string? Text = null,
    string? FilePath = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public enum WindowCliAction
{
    Active,
    List,
    Search,
    Wait,
    Focus,
    Close,
    Move,
    Resize,
    Center,
    Maximize,
    Fullscreen,
    Float,
    WorkspaceGet,
    WorkspaceSwitch,
    WorkspaceMoveActive,
    WorkspaceMoveWindow
}

public enum WindowSelectorKind
{
    Address,
    Title,
    Class
}

public sealed record WindowSelector(WindowSelectorKind Kind, string Value);

public sealed record WindowCliOptions(
    WindowCliAction Action,
    WindowSelector? Selector = null,
    int? X = null,
    int? Y = null,
    int? Width = null,
    int? Height = null,
    int? TimeoutMs = null,
    string? WorkspaceName = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public enum ScreenCliAction
{
    Pixel,
    WaitColor,
    SearchColor
}

public sealed record ScreenCliOptions(
    ScreenCliAction Action,
    int X,
    int Y,
    ScreenPixelColor? ExpectedColor = null,
    bool Relative = false,
    int? X2 = null,
    int? Y2 = null,
    int? TimeoutMs = null,
    int Tolerance = 0,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
