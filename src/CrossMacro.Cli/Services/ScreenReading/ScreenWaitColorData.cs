namespace CrossMacro.Cli.Services.ScreenReading;

public sealed record ScreenWaitColorData(int X, int Y, string ExpectedColor, string ActualColor, string ProviderName, bool Matched, int? TimeoutMs);
