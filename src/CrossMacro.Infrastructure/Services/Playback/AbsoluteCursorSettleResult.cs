namespace CrossMacro.Infrastructure.Services.Playback;

internal readonly record struct AbsoluteCursorSettleResult(
    bool IsSettled,
    (int X, int Y)? LastObservedPosition);
