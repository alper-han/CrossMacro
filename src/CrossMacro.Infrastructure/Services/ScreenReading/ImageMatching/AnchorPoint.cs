using System.Runtime.InteropServices;

namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct AnchorPoint(int X, int Y);
