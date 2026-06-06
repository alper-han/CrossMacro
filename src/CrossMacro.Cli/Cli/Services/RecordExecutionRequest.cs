namespace CrossMacro.Cli.Services;

public sealed class RecordExecutionRequest
{
    public string OutputFilePath { get; init; } = string.Empty;
    public bool RecordMouse { get; init; } = true;
    public bool RecordKeyboard { get; init; } = true;
    public bool RecordGamepad { get; init; } = false;
    public RecordCoordinateMode CoordinateMode { get; init; } = RecordCoordinateMode.Auto;
    public bool SkipInitialZero { get; init; }
    public int DurationSeconds { get; init; }
}
