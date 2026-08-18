namespace CrossMacro.Platform.Abstractions;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly record struct ScreenPoint(int X, int Y);
