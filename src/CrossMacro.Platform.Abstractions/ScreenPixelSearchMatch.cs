namespace CrossMacro.Platform.Abstractions;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly record struct ScreenPixelSearchMatch(ScreenPoint Point, ScreenPixelColor Color);
