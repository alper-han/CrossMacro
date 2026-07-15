namespace CrossMacro.Cli.Services;

public sealed record class ScreenWaitColorData(int X, int Y, string ExpectedColor, string ActualColor, string ProviderName, bool Matched, int? TimeoutMs);
