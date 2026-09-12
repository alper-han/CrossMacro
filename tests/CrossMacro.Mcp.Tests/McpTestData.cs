namespace CrossMacro.Mcp.Tests;

internal static class McpTestData
{
    internal static string GetPhysicalTemporaryRoot() =>
        OperatingSystem.IsMacOS() ? "/private/tmp" : Path.GetTempPath();

    internal static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(GetPhysicalTemporaryRoot(), $"crossmacro-mcp-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        return directory;
    }

    internal static string CreateTemporaryMacroFile()
    {
        var path = Path.Combine(GetPhysicalTemporaryRoot(), $"crossmacro-mcp-{Guid.NewGuid():N}.macro");
        File.WriteAllText(path, "macro");
        return path;
    }

    internal static string CreateTemporaryPngFile()
    {
        var path = Path.Combine(GetPhysicalTemporaryRoot(), $"crossmacro-mcp-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, [137, 80, 78, 71]);
        return path;
    }

    internal static byte[] CreatePngBytes() => [137, 80, 78, 71, 13, 10, 26, 10];

    internal static async Task<JsonElement> WaitForAutomationCompletionAsync(
        McpAutomationTools tools,
        string operationId,
        int maximumAttempts = 100)
    {
        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            var result = tools.GetAutomation(operationId);
            var structured = Assert.IsType<JsonElement>(result.StructuredContent);
            var operation = structured.GetProperty("operation");
            if (operation.GetProperty("state").GetString() is not "running")
            {
                return structured;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), TimeProvider.System, CancellationToken.None).ConfigureAwait(false);
        }

        throw new TimeoutException("Automation operation did not complete.");
    }

    internal static ScreenFrame CreateImageFrame() => new(
        new ScreenRect(0, 0, 2, 1),
        stride: 6,
        ScreenPixelFormat.Rgb24,
        new byte[] { 0, 0, 0, 0, 0, 0 });

    internal static WindowInfoData CreateWindow(int index)
    {
        return new WindowInfoData(
            Address: $"0x{index.ToString("x", System.Globalization.CultureInfo.InvariantCulture)}",
            Title: $"Window {index.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            Class: "TestApp",
            Pid: index,
            Workspace: "workspace",
            IsFocused: index is 0,
            IsFullscreen: false,
            IsMaximized: false,
            IsFloating: false,
            IsPinned: false,
            IsHidden: false,
            X: index,
            Y: index,
            Width: 800,
            Height: 600);
    }
}
