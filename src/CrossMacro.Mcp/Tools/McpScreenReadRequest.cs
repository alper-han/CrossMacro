namespace CrossMacro.Mcp.Tools;

internal sealed record McpScreenReadRequest(string Mode, int X, int Y, ScreenPixelColor? ExpectedColor = null,
    ScreenRect? Region = null, int Tolerance = 0, int? TimeoutMs = null);
