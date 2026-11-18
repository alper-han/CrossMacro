
namespace CrossMacro.Cli.Serialization;

public sealed record MacroInfoData(
    string MacroPath,
    string MacroName,
    DateTime CreatedAt,
    int EventCount,
    long TotalDurationMs,
    string CoordinateMode,
    bool IsAbsoluteCoordinates,
    bool SkipInitialZeroZero,
    long TrailingDelayMicroseconds,
    int TrailingDelayMs,
    bool HasTrailingRandomDelay,
    int TrailingDelayMinMs,
    int TrailingDelayMaxMs,
    MacroEventBreakdownData EventBreakdown
);
