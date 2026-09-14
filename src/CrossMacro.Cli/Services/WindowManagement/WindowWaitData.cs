namespace CrossMacro.Cli.Services.WindowManagement;

public sealed record WindowWaitData(bool Found, WindowInfoData? Window, int TimeoutMs);
