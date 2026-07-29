
namespace CrossMacro.Infrastructure.Services.ScreenReading;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly record struct ScreenImageMatch(ScreenPoint Point, double Score, int MatchedWidth = 0, int MatchedHeight = 0);
