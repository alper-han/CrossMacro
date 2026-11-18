
namespace CrossMacro.Cli.Serialization;

public sealed record RecordExecutionData(
    string OutputPath,
    int EventCount,
    long TotalDurationMs,
    bool RecordMouse,
    bool RecordKeyboard,
    string RequestedMode,
    string ActualMode,
    bool SkipInitialZero
);
