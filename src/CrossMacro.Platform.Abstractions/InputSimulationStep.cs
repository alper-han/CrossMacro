namespace CrossMacro.Platform.Abstractions;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly record struct InputSimulationStep(
    ushort Type,
    ushort Code,
    int Value,
    long DelayAfterMicroseconds = 0);
