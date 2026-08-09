namespace CrossMacro.Platform.Abstractions;

/// <summary>Represents an absolute target and its following microsecond delay.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly record struct AbsoluteMotionTrajectorySample(
    int X,
    int Y,
    long DelayAfterMicroseconds);
