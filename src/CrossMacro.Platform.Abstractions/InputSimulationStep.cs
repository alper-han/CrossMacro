namespace CrossMacro.Platform.Abstractions;

public readonly record struct InputSimulationStep(
    ushort Type,
    ushort Code,
    int Value,
    int DelayAfterMs = 0);
