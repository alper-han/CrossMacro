namespace CrossMacro.Cli.Services;

public sealed record WindowWaitData(bool Found, WindowInfoData? Window, int TimeoutMs);
