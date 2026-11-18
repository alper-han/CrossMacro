namespace CrossMacro.Commands.Options;

public sealed record RecordOptions(
    string OutputFilePath,
    bool RecordMouse = true,
    bool RecordKeyboard = true,
    RecordCoordinateMode CoordinateMode = RecordCoordinateMode.Auto,
    bool SkipInitialZero = false,
    int DurationSeconds = 0,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CommandOptions(JsonOutput, LogLevel);
