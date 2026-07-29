
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
        int w = User32.GetSystemMetrics(User32.SM_CXSCREEN);
        int h = User32.GetSystemMetrics(User32.SM_CYSCREEN);

        if (w > 0 && h > 0)
        {
            return Task.FromResult<(int Width, int Height)?>((w, h));
        }
        return Task.FromResult<(int Width, int Height)?>(null);
    }

    public void Dispose() { /* Empty */ }
}
