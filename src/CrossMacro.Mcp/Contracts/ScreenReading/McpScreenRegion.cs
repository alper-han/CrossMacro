namespace CrossMacro.Mcp.Contracts.ScreenReading;

/// <summary>
/// A bounded, end-exclusive desktop rectangle.
/// </summary>
public sealed record McpScreenRegion(int X, int Y, int Width, int Height);
