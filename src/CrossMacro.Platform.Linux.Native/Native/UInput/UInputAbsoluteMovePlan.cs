namespace CrossMacro.Platform.Linux.Native.UInput;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UInputAbsoluteMovePlan(
    (int X, int Y) Target,
    (int X, int Y)? Reassertion);
