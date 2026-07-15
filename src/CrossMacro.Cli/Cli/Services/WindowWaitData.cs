namespace CrossMacro.Cli.Services;

public sealed record class WindowWaitData(bool Found, WindowInfoData? Window, int TimeoutMs);
