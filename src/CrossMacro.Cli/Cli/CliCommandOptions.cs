using System.Collections.Generic;
using CrossMacro.Core.Models;
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

public sealed record SettingsGetCliOptions(string? Key = null, bool JsonOutput = false, string? LogLevel = null, bool All = false)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record SettingsSetCliOptions(string Key, string Value, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record SettingsListKeysCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record SettingsResetCliOptions(string Key, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public enum ProfileCliAction
{
    List,
    Current,
    Create,
    Switch,
    Rename,
    Delete
}

public sealed record ProfileCliOptions(
    ProfileCliAction Action,
    string? ProfileIdentifier = null,
    string? NewName = null,
    bool Force = false,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public enum TextExpansionCliAction
{
    List,
    Add,
    Remove,
    Enable,
    Disable,
    Test
}

public sealed record TextExpansionCliOptions(
    TextExpansionCliAction Action,
    string? Trigger = null,
    string? Replacement = null,
    PasteMethod Method = PasteMethod.CtrlV,
    TextInsertionMode InsertionMode = TextInsertionMode.Paste,
    DirectTypingMethod DirectTypingMethod = DirectTypingMethod.FastBatch,
    string? ProfileIdentifier = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record ScheduleListCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record ScheduleRunCliOptions(string TaskId, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public enum ScheduleCliAction
{
    Add,
    Edit,
    Remove,
    Enable,
    Disable,
    Next
}

public sealed record ScheduleCliOptions(
    ScheduleCliAction Action,
    string? TaskId = null,
    string? Name = null,
    string? MacroFilePath = null,
    string? Interval = null,
    string? At = null,
    string? Weekly = null,
    string? Time = null,
    double? Speed = null,
    bool? Enabled = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record ShortcutListCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public sealed record ShortcutRunCliOptions(string TaskId, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

public enum ShortcutCliAction
{
    Add,
    Edit,
    Remove,
    Enable,
    Disable,
    Bind
}

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
    string? LogLevel = null)
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

public enum ScreenshotCliAction
{
    Capture
}

public sealed record ScreenshotCliOptions(
    ScreenshotCliAction Action,
    string? OutputPath = null,
    bool Clipboard = false,
    int? RegionX = null,
    int? RegionY = null,
    int? RegionWidth = null,
    int? RegionHeight = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);

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
