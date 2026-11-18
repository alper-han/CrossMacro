
namespace CrossMacro.Cli.Serialization;

public sealed record MacroSummaryData(
    string MacroPath,
    string MacroName,
    int EventCount,
    long TotalDurationMs,
    string CoordinateMode,
    bool IsAbsoluteCoordinates
);
