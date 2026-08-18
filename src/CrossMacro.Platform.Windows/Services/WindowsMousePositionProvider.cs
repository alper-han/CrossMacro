
namespace CrossMacro.Platform.Windows.Services;

public sealed class WindowsMousePositionProvider : IMousePositionProvider
{
    public string ProviderName => "Windows GetCursorPos";
    public bool IsSupported => OperatingSystem.IsWindows();

    public Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        if (User32.GetCursorPos(out PointStruct pt))
        {
            return Task.FromResult<(int X, int Y)?>((pt.x, pt.y));
        }
        return Task.FromResult<(int X, int Y)?>(null);
    }

    public Task<(int Width, int Height)?> GetScreenResolutionAsync()
    {
        var bounds = QueryDesktopBounds();
        return Task.FromResult<(int Width, int Height)?>(bounds is not null
            ? (bounds.Value.Width, bounds.Value.Height)
            : null);
    }

    public Task<ScreenRect?> GetDesktopBoundsAsync()
    {
        return Task.FromResult(QueryDesktopBounds());
    }

    internal static ScreenRect? ReadDesktopBounds(Func<int, int> getSystemMetric)
    {
        ArgumentNullException.ThrowIfNull(getSystemMetric);

        int x = getSystemMetric(User32.SM_XVIRTUALSCREEN);
        int y = getSystemMetric(User32.SM_YVIRTUALSCREEN);
        int width = getSystemMetric(User32.SM_CXVIRTUALSCREEN);
        int height = getSystemMetric(User32.SM_CYVIRTUALSCREEN);
        return width > 0 && height > 0 ? new ScreenRect(x, y, width, height) : null;
    }

    private static ScreenRect? QueryDesktopBounds() => ReadDesktopBounds(User32.GetSystemMetrics);

    public void Dispose() { /* Empty */ }
}
