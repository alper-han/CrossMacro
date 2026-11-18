namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

internal readonly record struct AutomaticCandidate(int X, int Y, double Score, string Profile, int Width = 0, int Height = 0, double Scale = 1.0, double Coverage = 1.0, double EffectivePixels = 0.0);
