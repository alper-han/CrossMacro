namespace CrossMacro.Mcp.Tools;

internal sealed record McpScreenReadRequest(McpScreenReadMode Mode, int X, int Y, ScreenPixelColor? ExpectedColor = null,
    ScreenRect? Region = null, int Tolerance = 0, int? TimeoutMs = null)
{
    internal string ModeToken => Mode switch
    {
        McpScreenReadMode.Pixel => "pixel",
        McpScreenReadMode.WaitColor => "wait_color",
        McpScreenReadMode.SearchColor => "search_color",
        _ => throw new InvalidOperationException("Unknown screen read mode."),
    };
}
