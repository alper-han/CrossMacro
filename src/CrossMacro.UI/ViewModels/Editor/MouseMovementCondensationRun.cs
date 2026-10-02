namespace CrossMacro.UI.ViewModels.Editor;

internal sealed record MouseMovementCondensationRun(
    int StartIndex,
    int EndIndex,
    int FinalMoveIndex,
    int RemovedMoveCount,
    int RemovedDelayCount,
    long RemovedDelayMicroseconds);
