namespace CrossMacro.Commands.WindowManagement;

public sealed record WindowWaitData(bool Found, WindowInfoData? Window, int TimeoutMs);
