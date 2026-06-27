using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CrossMacro.Cli.Serialization;

public sealed record CliOutputEnvelope(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data")] JsonNode? Data,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings,
    [property: JsonPropertyName("errors")] IReadOnlyList<string> Errors
);

public sealed record DoctorCommandData(
    [property: JsonPropertyName("checks")] IReadOnlyList<DoctorCheckOutput> Checks
);

public sealed record DoctorCheckOutput(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("details")] JsonNode? Details
);

public sealed record RunScriptExecutionData(
    [property: JsonPropertyName("stepCount")] int StepCount,
    [property: JsonPropertyName("eventCount")] int EventCount,
    [property: JsonPropertyName("totalDurationMs")] long TotalDurationMs,
    [property: JsonPropertyName("initialDelayMs")] int InitialDelayMs,
    [property: JsonPropertyName("initialHasRandomDelay")] bool InitialHasRandomDelay,
    [property: JsonPropertyName("initialRandomDelayMinMs")] int InitialRandomDelayMinMs,
    [property: JsonPropertyName("initialRandomDelayMaxMs")] int InitialRandomDelayMaxMs,
    [property: JsonPropertyName("trailingDelayMs")] int TrailingDelayMs,
    [property: JsonPropertyName("coordinateMode")] string CoordinateMode,
    [property: JsonPropertyName("runtimeVariables")] IReadOnlyDictionary<string, string> RuntimeVariables
);

public sealed record MacroSummaryData(
    [property: JsonPropertyName("macroPath")] string MacroPath,
    [property: JsonPropertyName("macroName")] string MacroName,
    [property: JsonPropertyName("eventCount")] int EventCount,
    [property: JsonPropertyName("totalDurationMs")] long TotalDurationMs,
    [property: JsonPropertyName("coordinateMode")] string CoordinateMode,
    [property: JsonPropertyName("isAbsoluteCoordinates")] bool IsAbsoluteCoordinates
);

public sealed record MacroEventBreakdownData(
    [property: JsonPropertyName("mouseMove")] int MouseMove,
    [property: JsonPropertyName("buttonPress")] int ButtonPress,
    [property: JsonPropertyName("buttonRelease")] int ButtonRelease,
    [property: JsonPropertyName("click")] int Click,
    [property: JsonPropertyName("keyPress")] int KeyPress,
    [property: JsonPropertyName("keyRelease")] int KeyRelease
);

public sealed record MacroInfoData(
    [property: JsonPropertyName("macroPath")] string MacroPath,
    [property: JsonPropertyName("macroName")] string MacroName,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("eventCount")] int EventCount,
    [property: JsonPropertyName("totalDurationMs")] long TotalDurationMs,
    [property: JsonPropertyName("coordinateMode")] string CoordinateMode,
    [property: JsonPropertyName("isAbsoluteCoordinates")] bool IsAbsoluteCoordinates,
    [property: JsonPropertyName("skipInitialZeroZero")] bool SkipInitialZeroZero,
    [property: JsonPropertyName("trailingDelayMs")] int TrailingDelayMs,
    [property: JsonPropertyName("hasTrailingRandomDelay")] bool HasTrailingRandomDelay,
    [property: JsonPropertyName("trailingDelayMinMs")] int TrailingDelayMinMs,
    [property: JsonPropertyName("trailingDelayMaxMs")] int TrailingDelayMaxMs,
    [property: JsonPropertyName("eventBreakdown")] MacroEventBreakdownData EventBreakdown
);

public sealed record RecordExecutionData(
    [property: JsonPropertyName("outputPath")] string OutputPath,
    [property: JsonPropertyName("eventCount")] int EventCount,
    [property: JsonPropertyName("totalDurationMs")] long TotalDurationMs,
    [property: JsonPropertyName("recordMouse")] bool RecordMouse,
    [property: JsonPropertyName("recordKeyboard")] bool RecordKeyboard,
    [property: JsonPropertyName("requestedMode")] string RequestedMode,
    [property: JsonPropertyName("actualMode")] string ActualMode,
    [property: JsonPropertyName("skipInitialZero")] bool SkipInitialZero
);

public sealed record HeadlessRuntimeData(
    [property: JsonPropertyName("globalHotkeys")] bool GlobalHotkeys,
    [property: JsonPropertyName("scheduler")] bool Scheduler,
    [property: JsonPropertyName("shortcuts")] bool Shortcuts,
    [property: JsonPropertyName("textExpansion")] bool TextExpansion,
    [property: JsonPropertyName("hotkeyActions")] bool HotkeyActions
);

public sealed record TaskListData<T>(
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("tasks")] IReadOnlyList<T> Tasks
);

public sealed record ScheduleTaskData(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("macroFilePath")] string MacroFilePath,
    [property: JsonPropertyName("weeklyDays"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WeeklyDays,
    [property: JsonPropertyName("weeklyTime"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WeeklyTime,
    [property: JsonPropertyName("nextRunTime")] DateTime? NextRunTime,
    [property: JsonPropertyName("lastRunTime")] DateTime? LastRunTime,
    [property: JsonPropertyName("lastStatus")] string? LastStatus
);

public sealed record ShortcutTaskData(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("hotkey")] string Hotkey,
    [property: JsonPropertyName("macroFilePath")] string MacroFilePath,
    [property: JsonPropertyName("playbackSpeed")] double PlaybackSpeed,
    [property: JsonPropertyName("loopEnabled")] bool LoopEnabled,
    [property: JsonPropertyName("runWhileHeld")] bool RunWhileHeld,
    [property: JsonPropertyName("repeatCount")] int RepeatCount,
    [property: JsonPropertyName("repeatDelayMs")] int RepeatDelayMs,
    [property: JsonPropertyName("lastTriggeredTime")] DateTime? LastTriggeredTime,
    [property: JsonPropertyName("lastStatus")] string? LastStatus
);

public sealed record ShortcutTaskRunData(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("hotkey")] string Hotkey,
    [property: JsonPropertyName("macroFilePath")] string MacroFilePath,
    [property: JsonPropertyName("lastTriggeredTime")] DateTime? LastTriggeredTime,
    [property: JsonPropertyName("lastStatus")] string? LastStatus
);

public sealed record ScheduleTaskRunData(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("macroFilePath")] string MacroFilePath,
    [property: JsonPropertyName("lastRunTime")] DateTime? LastRunTime,
    [property: JsonPropertyName("lastStatus")] string? LastStatus
);
